locals {
  tags = { project = "guestflow", managed_by = "terraform" }
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.prefix}"
  location = var.location
  tags     = local.tags
}

# ---- Logging (container logs + metrics land here) ---------------------------------
resource "azurerm_log_analytics_workspace" "logs" {
  name                = "log-${var.prefix}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# ---- Azure SQL (serverless, auto-pauses when idle to keep cost near zero) --------
resource "random_string" "suffix" {
  length  = 5
  upper   = false
  special = false
}

resource "azurerm_mssql_server" "sql" {
  name                         = "sql-${var.prefix}-${random_string.suffix.result}"
  resource_group_name          = azurerm_resource_group.rg.name
  location                     = azurerm_resource_group.rg.location
  version                      = "12.0"
  administrator_login          = "guestflowadmin"
  administrator_login_password = var.sql_admin_password
  minimum_tls_version          = "1.2"
  tags                         = local.tags
}

resource "azurerm_mssql_database" "db" {
  name                        = "GuestFlow"
  server_id                   = azurerm_mssql_server.sql.id
  sku_name                    = "GP_S_Gen5_1"
  min_capacity                = 0.5
  auto_pause_delay_in_minutes = 60
  max_size_gb                 = 2
  tags                        = local.tags
}

# Allow Azure services (the Container Apps environment) to reach SQL
resource "azurerm_mssql_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_mssql_server.sql.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# ---- Container Apps ------------------------------------------------------------------
resource "azurerm_container_app_environment" "env" {
  name                       = "cae-${var.prefix}"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.logs.id
  tags                       = local.tags
}

# API + Redis sidecar in one app: they share localhost, so the API reaches Redis at localhost:6379
resource "azurerm_container_app" "api" {
  name                         = "ca-${var.prefix}-api"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"
  tags                         = local.tags

  secret {
    name  = "sql-connection"
    value = "Server=tcp:${azurerm_mssql_server.sql.fully_qualified_domain_name},1433;Database=${azurerm_mssql_database.db.name};User ID=guestflowadmin;Password=${var.sql_admin_password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60"
  }

  template {
    min_replicas = 1
    max_replicas = 3

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "ConnectionStrings__Sql"
        secret_name = "sql-connection"
      }
      env {
        name  = "ConnectionStrings__Redis"
        value = "localhost:6379"
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }

    container {
      name   = "redis"
      image  = "docker.io/library/redis:7-alpine"
      cpu    = 0.25
      memory = "0.5Gi"
    }

    # Scale out when traffic grows (front-desk screens poll frequently)
    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = "50"
    }
  }

  ingress {
    external_enabled = false # only the web app can call the API
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

resource "azurerm_container_app" "web" {
  name                         = "ca-${var.prefix}-web"
  container_app_environment_id = azurerm_container_app_environment.env.id
  resource_group_name          = azurerm_resource_group.rg.name
  revision_mode                = "Single"
  tags                         = local.tags

  template {
    min_replicas = 0
    max_replicas = 2

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "API_UPSTREAM"
        value = "http://${azurerm_container_app.api.name}"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 80
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}
