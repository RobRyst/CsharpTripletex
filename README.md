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

## 🚀 Tech Stack
### ⚙️ Backend (API & Sync)
- .NET 8 / C# – ASP.NET Core Web API

- Entity Framework Core – SQLite for lightweight data storage

- HttpClient – Communication with Tripletex REST API

- System.Text.Json – JSON (de)serialization

- ILogger – Centralized logging across services

## ✨ Features
### 🧾 Order & Invoice Automation
Create sales orders directly in Tripletex from the API

Instantly generate and send invoices from orders

Automatically update status and Tripletex IDs in local DB

### 📎 Attachment Integration
- Uploads invoice.pdf to associated voucher (bilag)

- Ensures correct linking of attachments via voucherId

- Verifies upload success and logs outcomes

### 🔄 Syncing Mechanism
- Pulls and stores:

## 🧾 Invoices from Tripletex

## 📦 Sales orders

- Performs upsert (insert or update)

- Connects invoice ↔ voucher ↔ attachment

## ⚠️ Robust Logging & Error Handling
- Logs full error details on API failures

- Handles edge cases in approval and sending steps

- Structured log output with status codes and messages

## 🧱 Architecture
- Service Layer – SaleOrderService, InvoiceService

- Mapping Layer – Maps between DTOs, entities, and Tripletex formats

- Repository Layer – Handles SQLite persistence

- Tripletex Integration Layer – Handles:

- Invoice creation

- Order-to-invoice conversion

- PDF upload to voucher

- Voucher lookups

---

## 🧠 What I Learned
Building TripletexSync deepened my understanding of API integrations, automation workflows, and data synchronization between external systems and local databases. This project focused on connecting a business-critical accounting system (Tripletex) with custom logic and reliable syncing. Here’s what I gained from it:

### ASP.NET Core Web API Design
- I strengthened my skills in designing RESTful APIs using ASP.NET Core, learning how to organize controllers, services, and middleware for a clean and scalable backend application.

### Entity Framework Core & SQLite
- I applied EF Core to manage lightweight local persistence with SQLite, and practiced upsert operations and change tracking to ensure accurate data syncing and version control.

### External API Integration with HttpClient
- Communicating with the Tripletex REST API taught me how to structure request/response flows, manage authentication headers, and handle rate limits and timeouts effectively.

### Order & Invoice Automation
- Automating the creation and delivery of sales orders and invoices sharpened my understanding of business process flows and how to implement them in a real-world, API-driven context.

### File Upload & Attachment Linking
- Uploading invoice PDFs and linking them to vouchers introduced me to multipart form-data handling, validation, and correct use of API contracts for file management.

### Syncing & Data Consistency
- I developed mechanisms to pull and synchronize external data (orders, invoices) with local storage, improving my knowledge of idempotent operations and reliable state management.

### Mapping & Data Transformation
- Mapping between local models, DTOs, and Tripletex formats gave me practical experience abstracting data transformation logic for maintainability and reuse.

### Logging & Error Handling
- Implementing robust logging with ILogger helped me understand how to capture and classify errors for better observability and troubleshooting. I learned to log status codes, trace sync failures, and handle edge cases without disrupting the overall pipeline.

### Layered Architecture & Clean Code
- Structuring the solution with service, repository, and integration layers reinforced architectural best practices like separation of concerns and dependency injection, resulting in a testable and extensible codebase.
