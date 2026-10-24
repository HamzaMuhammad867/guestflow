# ---- Alerts: email the on-call engineer when the API misbehaves ------------------------
resource "azurerm_monitor_action_group" "oncall" {
  name                = "ag-${var.prefix}-oncall"
  resource_group_name = azurerm_resource_group.rg.name
  short_name          = "gf-oncall"

  email_receiver {
    name          = "on-call"
    email_address = var.alert_email
  }
}

# More than 10 server errors (HTTP 5xx) in 5 minutes
resource "azurerm_monitor_metric_alert" "api_5xx" {
  name                = "alert-${var.prefix}-api-5xx"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_container_app.api.id]
  description         = "GuestFlow API is returning server errors"
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.App/containerApps"
    metric_name      = "Requests"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 10

    dimension {
      name     = "statusCodeCategory"
      operator = "Include"
      values   = ["5xx"]
    }
  }

  action {
    action_group_id = azurerm_monitor_action_group.oncall.id
  }
}

# Container keeps restarting (crash loop / failed health checks)
resource "azurerm_monitor_metric_alert" "api_restarts" {
  name                = "alert-${var.prefix}-api-restarts"
  resource_group_name = azurerm_resource_group.rg.name
  scopes              = [azurerm_container_app.api.id]
  description         = "GuestFlow API container is restarting"
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.App/containerApps"
    metric_name      = "RestartCount"
    aggregation      = "Maximum"
    operator         = "GreaterThan"
    threshold        = 2
  }

  action {
    action_group_id = azurerm_monitor_action_group.oncall.id
  }
}
