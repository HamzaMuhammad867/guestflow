output "web_url" {
  description = "Public URL of the GuestFlow web app"
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "sql_server" {
  value = azurerm_mssql_server.sql.fully_qualified_domain_name
}
