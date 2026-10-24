variable "prefix" {
  description = "Short name used in every resource name"
  type        = string
  default     = "guestflow"
}

variable "location" {
  description = "Azure region (UAE North is closest to Ras Al Khaimah)"
  type        = string
  default     = "uaenorth"
}

variable "api_image" {
  description = "Public container image for the API, e.g. ghcr.io/<github-user>/guestflow-api:latest"
  type        = string
}

variable "web_image" {
  description = "Public container image for the web front end, e.g. ghcr.io/<github-user>/guestflow-web:latest"
  type        = string
}

variable "sql_admin_password" {
  description = "Azure SQL admin password (12+ chars, upper/lower/digit/symbol)"
  type        = string
  sensitive   = true
}

variable "alert_email" {
  description = "Where monitoring alerts are sent"
  type        = string
}
