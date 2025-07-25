# 💼 TripletexSync – Order & Invoice Automation
Integration between a local database and the Tripletex API for managing orders, invoices, and attachments.
Built with ASP.NET Core, Entity Framework, and C#.

## 📌 Project Overview
TripletexSync is a full-stack API integration for automating:

- 📦 Creation of sales orders directly in Tripletex

- 🧾 Automatic invoice generation and delivery

- 📎 Uploading PDF attachments to vouchers

- 🔁 Syncing data between Tripletex and local storage

- 🗃️ Local persistence of customer, order, and invoice data

##🚀 Tech Stack
###⚙️ Backend (API & Sync)
- .NET 8 / C# – ASP.NET Core Web API

- Entity Framework Core – SQLite for lightweight data storage

- HttpClient – Communication with Tripletex REST API

- System.Text.Json – JSON (de)serialization

- ILogger – Centralized logging across services

##✨ Features
###🧾 Order & Invoice Automation
Create sales orders directly in Tripletex from the API

Instantly generate and send invoices from orders

Automatically update status and Tripletex IDs in local DB

###📎 Attachment Integration
- Uploads invoice.pdf to associated voucher (bilag)

- Ensures correct linking of attachments via voucherId

- Verifies upload success and logs outcomes

###🔄 Syncing Mechanism
- Pulls and stores:

##🧾 Invoices from Tripletex

##📦 Sales orders

- Performs upsert (insert or update)

- Connects invoice ↔ voucher ↔ attachment

##⚠️ Robust Logging & Error Handling
- Logs full error details on API failures

- Handles edge cases in approval and sending steps

- Structured log output with status codes and messages

##🧱 Architecture
- Service Layer – SaleOrderService, InvoiceService

- Mapping Layer – Maps between DTOs, entities, and Tripletex formats

- Repository Layer – Handles SQLite persistence

- Tripletex Integration Layer – Handles:

- Invoice creation

- Order-to-invoice conversion

- PDF upload to voucher

- Voucher lookups
