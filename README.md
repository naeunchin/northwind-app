# Northwind Sales Portal Web App

## Overview
A mock enterprise system for order and product inventory management built using .NET 8 Blazor Web App. This application allows Northwind enterprise employees to view, create, edit, and manage corporate order ledgers and their associated shopping-cart line items.  

It demonstrates clean architecture, separated service layers, and interactive UI components to emulate real-world enterprise sales tracking.

## Key Features
* **Clean Architecture:** Separate front-end and back-end code for organization.
* **Interactive UI:** Utilizes MudBlazor for responsive grids, dynamic dialog modals, and form validation.
* **Smart Data Tracking:** Implements real-time cart calculations and safely manages database tracking states using Entity Framework Core.
* **Result Pattern Handling:** Uses a custom `Result<T>` wrapper in the Business Logic Layer (BLL) to cleanly pass data and catch exceptions without crashing the UI.

## App Demo 


## Tech Stack & Dependencies 
* **Framework:** .NET 8 (Blazor Web App - Interactive Server)
* **Backend:** C#, Entity Framework Core
* **Database:** Microsoft SQL Server
* **UI Library:** MudBlazor (v7)

## Installation & Setup
To run this project locally on your machine, follow these steps:

1. **Clone the repository:**
   ```bash
   git clone [https://github.com/YourUsername/YourRepoName.git](https://github.com/YourUsername/YourRepoName.git)
   ```
2. **Setup the Database:** Locate the Northwind .bacpac file inside the /Database folder of this repository. Import the .bacpac file into your local instance of Microsoft SQL Server (using SQL Server Management Studio or Azure Data Studio).
3. **Configure the Connection String:** Open appsettings.json in the BlazorWebApp project. Update the NorthwindContext connection string to point to your local SQL Server instance.
4. Run the Application: Open the solution in Visual Studio 2022 and press F5, or run the following command in your terminal:
   ```bash
   dotnet run --project BlazorWebApp
   ```

## Unfinished Tasks
* Implement soft-delete logic (e.g., RemoveFromViewFlag) instead of hard-deleting database records.
* Add pagination and advanced filtering to the main Orders data grid.
* Build out a dedicated reporting dashboard for logistical metrics.

## Known Issues 
N/A  

© 2026 [Na Eun Chin]. All Rights Reserved.
This project is for demonstration purposes only as a personal project. Unauthorized copying, modification, or distribution of this codebase is strictly prohibited.
