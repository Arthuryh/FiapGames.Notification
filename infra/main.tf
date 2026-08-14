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

resource "azurerm_storage_container" "function_deployment" {
  name                  = "function-deployment"
  storage_account_id    = azurerm_storage_account.functions.id
  container_access_type = "private"
}

resource "azurerm_service_plan" "functions" {
  name                = "asp-fiapgames-notification-prod"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  os_type             = "Linux"
  sku_name            = "FC1"
  tags                = local.tags
}

resource "azurerm_function_app_flex_consumption" "notifications" {
  name                = "func-fcg-notify-${local.suffix}"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.location
  service_plan_id     = azurerm_service_plan.functions.id

  storage_container_type      = "blobContainer"
  storage_container_endpoint  = "${azurerm_storage_account.functions.primary_blob_endpoint}${azurerm_storage_container.function_deployment.name}"
  storage_authentication_type = "StorageAccountConnectionString"
  storage_access_key          = azurerm_storage_account.functions.primary_access_key

  runtime_name           = "dotnet-isolated"
  runtime_version        = "10.0"
  maximum_instance_count = 10
  instance_memory_in_mb  = 2048
  tags                   = local.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [data.azurerm_user_assigned_identity.workloads.id]
  }

  site_config {}

  app_settings = {
    "ASPNETCORE_ENVIRONMENT"                          = "Production"
    "AZURE_CLIENT_ID"                                 = data.azurerm_user_assigned_identity.workloads.client_id
    "KeyVaultUri"                                     = data.azurerm_key_vault.main.vault_uri
    "NotificationServiceBus__fullyQualifiedNamespace" = trimsuffix(trimprefix(azurerm_servicebus_namespace.notifications.endpoint, "https://"), ":443/")
    "NotificationServiceBus__credential"              = "managedidentity"
    "NotificationServiceBus__clientId"                = data.azurerm_user_assigned_identity.workloads.client_id
    "PaymentNotificationQueueName"                    = azurerm_servicebus_queue.payment.name
    "AuthenticationNotificationQueueName"             = azurerm_servicebus_queue.authentication.name
    "FUNCTIONS_WORKER_RUNTIME"                        = "dotnet-isolated"
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

output "function_app_name" { value = azurerm_function_app_flex_consumption.notifications.name }
output "service_bus_fully_qualified_namespace" { value = trimsuffix(trimprefix(azurerm_servicebus_namespace.notifications.endpoint, "https://"), ":443/") }
