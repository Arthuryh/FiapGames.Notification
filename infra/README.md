# Notification serverless infrastructure

This stack provisions the Notification Azure Function, Service Bus queues, storage and RBAC. Shared FCG resources are consumed as data sources.

```powershell
terraform init
terraform plan -var "subscription_id=<subscription-id>" -out serverless.tfplan
terraform apply serverless.tfplan
```
