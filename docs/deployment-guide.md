# Deployment Guide: Ez.Bank Claims API

## Azure Resources

Create a single Resource Group (e.g., `rg-ezbank-prod`) containing:

| Resource | Type | Notes |
|---|---|---|
| **Storage Account** | `st<env>ezbank` | General Purpose v2, Standard LRS (or GRS for prod). Hosts Table Storage, Blob Storage, and Functions internal state. |
| **Function App** | `func-ezbank-claims-<env>` | .NET 8 Isolated, Linux, Flex Consumption plan. |
| **Application Insights** | `appi-ezbank-<env>` | Connected to the Function App for logging and monitoring. |

| **Static Web App** | `stapp-ezbank-demo-<env>` | Hosts the React demo application. Free tier is sufficient for dev/staging. |

That's it for v1. No SQL Server, no Key Vault, no VNet.

### Storage Account Setup

The tables and blob container are created automatically at startup by the application (`DataSeeder` creates tables, `BlobDocumentStorage` creates the container). No manual table creation needed.

### Create Resources via Azure CLI

```bash
ENV="prod"
LOCATION="northeurope"
RG="rg-ezbank-$ENV"

# Resource Group
az group create --name $RG --location $LOCATION

# Storage Account
az storage account create \
  --name "st${ENV}ezbank" \
  --resource-group $RG \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Get the storage connection string (save this for later)
az storage account show-connection-string \
  --name "st${ENV}ezbank" \
  --resource-group $RG \
  --query connectionString -o tsv

# Function App (Flex Consumption)
az functionapp create \
  --name "func-ezbank-claims-$ENV" \
  --resource-group $RG \
  --storage-account "st${ENV}ezbank" \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux \
  --consumption-plan-location $LOCATION

# Application Insights
az monitor app-insights component create \
  --app "appi-ezbank-$ENV" \
  --resource-group $RG \
  --location $LOCATION \
  --kind web

# Link App Insights to Function App
APPINSIGHTS_KEY=$(az monitor app-insights component show \
  --app "appi-ezbank-$ENV" \
  --resource-group $RG \
  --query instrumentationKey -o tsv)

az functionapp config appsettings set \
  --name "func-ezbank-claims-$ENV" \
  --resource-group $RG \
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$APPINSIGHTS_KEY"

# Static Web App (React demo)
az staticwebapp create \
  --name "stapp-ezbank-demo-$ENV" \
  --resource-group $RG \
  --location $LOCATION \
  --sku Free
```

---

## Function App Settings

Set these on the Function App (via Portal > Configuration, or CLI):

```bash
az functionapp config appsettings set \
  --name "func-ezbank-claims-$ENV" \
  --resource-group $RG \
  --settings \
    "AUTH_PASSWORD=<strong-random-password>" \
    "JWT_SECRET=<random-string-at-least-32-chars>" \
    "JWT_EXPIRY_MINUTES=60" \
    "AZURE_STORAGE_CONNECTION=<storage-connection-string>" \
    "USE_LOCAL_STORAGE=false" \
    "BLOB_CONTAINER_NAME=claim-documents"
```

| Setting | Value |
|---|---|
| `AUTH_PASSWORD` | A strong password users must provide to get a JWT token. |
| `JWT_SECRET` | Random string, minimum 32 characters. Used to sign JWTs. |
| `JWT_EXPIRY_MINUTES` | `60` (or adjust as needed). |
| `AZURE_STORAGE_CONNECTION` | The full connection string from the Storage Account. |
| `USE_LOCAL_STORAGE` | `false` (use Blob Storage in Azure). |
| `BLOB_CONTAINER_NAME` | `claim-documents` |

Generate secure values:
```bash
# Generate a random password
openssl rand -base64 24

# Generate a JWT secret
openssl rand -base64 48
```

---

## GitHub Actions Deployment

### GitHub Secrets

Set these in your repo under **Settings > Secrets and variables > Actions**:

| Secret | Value | How to Get It |
|---|---|---|
| `AZURE_FUNCTIONAPP_PUBLISH_PROFILE` | The publish profile XML | Azure Portal > Function App > Overview > "Get publish profile" |
| `AZURE_FUNCTIONAPP_NAME` | `func-ezbank-claims-prod` | The name of your Function App |

Alternatively, if using Azure service principal authentication instead of publish profile:

| Secret | Value | How to Get It |
|---|---|---|
| `AZURE_CREDENTIALS` | Service principal JSON | `az ad sp create-for-rbac --name "github-deploy" --role contributor --scopes /subscriptions/<sub-id>/resourceGroups/<rg> --sdk-auth` |
| `AZURE_FUNCTIONAPP_NAME` | `func-ezbank-claims-prod` | The name of your Function App |

### Workflow File

Create `.github/workflows/api-ci.yml`:

```yaml
name: Deploy API

on:
  push:
    branches: [main]
    paths: ['api/**']
  workflow_dispatch:

env:
  DOTNET_VERSION: '8.0.x'
  PROJECT_PATH: 'api/Ez.Bank.FunctionsApi'
  TEST_PATH: 'api/Ez.Bank.UnitTests'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore
        run: dotnet restore api/Ez.Bank.sln

      - name: Build
        run: dotnet build api/Ez.Bank.sln --configuration Release --no-restore

      - name: Test
        run: dotnet test ${{ env.TEST_PATH }} --configuration Release --no-build

      - name: Publish
        run: dotnet publish ${{ env.PROJECT_PATH }} --configuration Release --output ./publish

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ secrets.AZURE_FUNCTIONAPP_NAME }}
          package: ./publish
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

### Demo App Workflow

Create `.github/workflows/demo-ci.yml`:

```yaml
name: Deploy Demo App

on:
  push:
    branches: [main]
    paths: ['demo/**']
  workflow_dispatch:

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Deploy to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: demo/ez-bank-demo
          output_location: dist
        env:
          VITE_API_BASE_URL: ${{ secrets.API_BASE_URL }}
```

The `Azure/static-web-apps-deploy` action uses Oryx to detect the Node.js project and run `npm install` + `npm run build` automatically. The `output_location` tells it where the build output is relative to `app_location`.

> **Important:** The `branches` trigger must match the production branch configured on the Static Web App in Azure. If your default branch is `master` instead of `main`, change it in both the workflow and the Azure resource (Portal > Static Web App > Configuration > Production branch).

### Demo App Configuration

The demo app needs to know the API URL in production. Set the `VITE_API_BASE_URL` environment variable at build time.

#### GitHub Secrets for the Demo App

| Secret | Value | How to Get It |
|---|---|---|
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Deployment token | Azure Portal > Static Web App > Overview > "Manage deployment token" |
| `API_BASE_URL` | `https://func-ezbank-claims-prod.azurewebsites.net/api` | The Function App URL with `/api` suffix |

#### API Proxy Configuration (Production)

In production the demo app runs on a different domain than the API, so CORS must be configured on the Function App:

```bash
az functionapp cors add \
  --name "func-ezbank-claims-$ENV" \
  --resource-group $RG \
  --allowed-origins "https://<static-web-app-hostname>"
```

Get the Static Web App hostname:
```bash
az staticwebapp show \
  --name "stapp-ezbank-demo-$ENV" \
  --resource-group $RG \
  --query "defaultHostname" -o tsv
```

#### Custom Domain (Optional)

```bash
az staticwebapp hostname set \
  --name "stapp-ezbank-demo-$ENV" \
  --resource-group $RG \
  --hostname "demo.ezbank.is"
```

---

## Deployment Checklist

### API
1. **Create Azure resources** (Resource Group, Storage Account, Function App, App Insights)
2. **Set Function App settings** (AUTH_PASSWORD, JWT_SECRET, AZURE_STORAGE_CONNECTION, etc.)
3. **Download publish profile** from Azure Portal
4. **Add GitHub secrets** (AZURE_FUNCTIONAPP_PUBLISH_PROFILE, AZURE_FUNCTIONAPP_NAME)
5. **Push to main** — the workflow builds, tests, and deploys automatically
6. **Verify** — hit `https://func-ezbank-claims-prod.azurewebsites.net/api/health`

### Demo App
7. **Create Static Web App** (via CLI or Portal)
8. **Get deployment token** from Azure Portal > Static Web App > "Manage deployment token"
9. **Add GitHub secrets** (AZURE_STATIC_WEB_APPS_API_TOKEN, API_BASE_URL)
10. **Configure CORS** on the Function App to allow the Static Web App origin
11. **Push to main** (changes in `demo/`) — the workflow builds and deploys automatically
12. **Verify** — open the Static Web App URL in a browser

---

## Manual Deployment (without CI/CD)

```bash
cd api/Ez.Bank.FunctionsApi
dotnet publish --configuration Release --output ./publish
cd publish
func azure functionapp publish func-ezbank-claims-prod
```

---

## Multiple Environments

Repeat the Azure resource creation for each environment (`dev`, `staging`, `prod`) with different resource names and settings. Use separate GitHub environments with environment-specific secrets, or create separate workflow files per environment.

| Environment | Resource Group | Storage Account | Function App | Static Web App |
|---|---|---|---|---|
| dev | `rg-ezbank-dev` | `stdevezbank` | `func-ezbank-claims-dev` | `stapp-ezbank-demo-dev` |
| staging | `rg-ezbank-staging` | `ststagingezbank` | `func-ezbank-claims-staging` | `stapp-ezbank-demo-staging` |
| prod | `rg-ezbank-prod` | `stprodezbank` | `func-ezbank-claims-prod` | `stapp-ezbank-demo-prod` |
