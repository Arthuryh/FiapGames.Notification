terraform {
  required_version = ">= 1.6.0"
  backend "azurerm" {}
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }
}

provider "azurerm" {
  features {}
  subscription_id = var.subscription_id
}

variable "subscription_id" {
  description = "Azure subscription containing the shared FCG resources."
  type        = string
}

variable "location" {
  type    = string
  default = "brazilsouth"
}

locals {
  resource_group_name = "rg-fiapgames-prod"
  suffix              = substr(replace(var.subscription_id, "-", ""), 0, 8)
  tags = {
    application = "fiap-cloud-games"
    component   = "notification-serverless"
    environment = "production"
    managed_by  = "terraform"
  }
}

data "azurerm_resource_group" "main" { name = local.resource_group_name }

data "azurerm_key_vault" "main" {
  name                = "kv-fiapgames-${local.suffix}"
  resource_group_name = data.azurerm_resource_group.main.name
}

data "azurerm_user_assigned_identity" "workloads" {
  name                = "id-fiapgames-workloads-prod"
  resource_group_name = data.azurerm_resource_group.main.name
}

resource "azurerm_servicebus_namespace" "notifications" {
  name                = "sb-fiapgames-notification-${local.suffix}"
  location            = var.location
  resource_group_name = data.azurerm_resource_group.main.name
  sku                 = "Basic"
  local_auth_enabled  = false
  tags                = local.tags
}

resource "azurerm_servicebus_queue" "payment" {
  name                                 = "notification-payment"
  namespace_id                         = azurerm_servicebus_namespace.notifications.id
  max_delivery_count                   = 5
  dead_lettering_on_message_expiration = true
  default_message_ttl                  = "P7D"
  lock_duration                        = "PT1M"
}

resource "azurerm_servicebus_queue" "authentication" {
  name                                 = "notification-authentication"
  namespace_id                         = azurerm_servicebus_namespace.notifications.id
  max_delivery_count                   = 5
  dead_lettering_on_message_expiration = true
  default_message_ttl                  = "P7D"
  lock_duration                        = "PT1M"
}

resource "azurerm_storage_account" "functions" {
  name                     = "stfcgnotify${local.suffix}"
  resource_group_name      = data.azurerm_resource_group.main.name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
  tags                     = local.tags
}

resource "azurerm_service_plan" "functions" {
  name                = "asp-fiapgames-notification-prod"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "Y1"
  tags                = local.tags
}

resource "azurerm_linux_function_app" "notifications" {
  name                        = "func-fiapgames-notification-${local.suffix}"
  resource_group_name         = data.azurerm_resource_group.main.name
  location                    = var.location
  service_plan_id             = azurerm_service_plan.functions.id
  storage_account_name        = azurerm_storage_account.functions.name
  storage_account_access_key  = azurerm_storage_account.functions.primary_access_key
  https_only                  = true
  functions_extension_version = "~4"
  tags                        = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [data.azurerm_user_assigned_identity.workloads.id]
  }

  site_config {
    application_stack {
      dotnet_version              = "10.0"
      use_dotnet_isolated_runtime = true
    }
    minimum_tls_version = "1.2"
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"                          = "Production"
    "KeyVaultUri"                                     = data.azurerm_key_vault.main.vault_uri
    "NotificationServiceBus__fullyQualifiedNamespace" = trimsuffix(trimprefix(azurerm_servicebus_namespace.notifications.endpoint, "https://"), ":443/")
    "NotificationServiceBus__credential"              = "managedidentity"
    "NotificationServiceBus__clientId"                = data.azurerm_user_assigned_identity.workloads.client_id
    "PaymentNotificationQueueName"                    = azurerm_servicebus_queue.payment.name
    "AuthenticationNotificationQueueName"             = azurerm_servicebus_queue.authentication.name
    "FUNCTIONS_WORKER_RUNTIME"                        = "dotnet-isolated"
    "WEBSITE_RUN_FROM_PACKAGE"                        = "1"
  }
}

resource "azurerm_role_assignment" "function_receiver" {
  scope                = azurerm_servicebus_namespace.notifications.id
  role_definition_name = "Azure Service Bus Data Receiver"
  principal_id         = data.azurerm_user_assigned_identity.workloads.principal_id
}

resource "azurerm_role_assignment" "workload_sender" {
  scope                = azurerm_servicebus_namespace.notifications.id
  role_definition_name = "Azure Service Bus Data Sender"
  principal_id         = data.azurerm_user_assigned_identity.workloads.principal_id
}

output "function_app_name" { value = azurerm_linux_function_app.notifications.name }
output "service_bus_fully_qualified_namespace" { value = trimsuffix(trimprefix(azurerm_servicebus_namespace.notifications.endpoint, "https://"), ":443/") }
