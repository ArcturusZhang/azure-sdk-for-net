# Azure Provisioning Libraries — Infrastructure Overview

This document describes the architecture, code generation process, and future plans of the Azure Provisioning libraries for .NET.

## What Are the Azure Provisioning Libraries?

The Azure Provisioning libraries allow developers to define Azure infrastructure declaratively in C#. Instead of writing Bicep or ARM templates by hand, you describe your resources in strongly-typed .NET code, which compiles down to Bicep and then deploys via tools such as the [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/get-started?tabs=localinstall&pivots=programming-language-csharp).

**Pipeline:** C# → Bicep → ARM JSON → Azure Deployment

All libraries ship as NuGet packages. Install a service-specific library and `Azure.Provisioning` is pulled in transitively:

```bash
dotnet add package Azure.Provisioning.Storage --prerelease
```

## Repository Layout

All provisioning code lives under [`sdk/provisioning/`](https://github.com/Azure/azure-sdk-for-net/tree/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning):

```
sdk/provisioning/
├── Azure.Provisioning/              # Core library
├── Azure.Provisioning.Storage/      # Resource Provider (RP) library – Storage
├── Azure.Provisioning.KeyVault/     # RP library – Key Vault
├── Azure.Provisioning.<Service>/    # … 26 more RP libraries
├── Azure.Provisioning.Deployment/   # Deployment helpers
├── Generator/                       # Code-generation tool (current)
└── Generator.sln
```

---

## Core Library — `Azure.Provisioning`

**Package:** [`Azure.Provisioning`](https://www.nuget.org/packages/Azure.Provisioning/) \
**Source:** [`sdk/provisioning/Azure.Provisioning/`](https://github.com/Azure/azure-sdk-for-net/tree/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning)

The core library provides the abstractions every RP library builds upon. Its main responsibilities are:

1. **Type system** — `BicepValue<T>`, `BicepList<T>`, `BicepDictionary<T>` wrappers that represent properties safely at design time even when actual values only exist at deployment time.
2. **Resource model** — base classes for Azure resources and nested configuration objects.
3. **Bicep compilation** — turning the in-memory object graph into `.bicep` source files via `Infrastructure` → `ProvisioningPlan`.
4. **Common resources** — generated classes for cross-cutting ARM concepts (resource groups, role assignments, managed identities, deployments, etc.).

### Class Hierarchy

```
Provisionable (abstract base)
├── ProvisionableConstruct            # Configuration / nested object
│   └── NamedProvisionableConstruct   # Named Bicep entity
│       ├── ProvisionableResource     # Azure resource (all RP resources inherit this)
│       ├── ProvisioningParameter     # Bicep parameter
│       ├── ProvisioningOutput        # Bicep output
│       ├── ProvisioningVariable      # Bicep variable
│       └── ModuleImport              # Imported Bicep module
└── Infrastructure                    # Top-level container → compiles to a Bicep module
```

Key source files:

| File | Purpose |
|------|---------|
| [`ProvisionableResource.cs`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/Primitives/ProvisionableResource.cs) | Base class for every Azure resource. Carries `ResourceType`, `ResourceVersion`, and Bicep metadata. |
| [`ProvisionableConstruct.cs`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/Primitives/ProvisionableConstruct.cs) | Base class for nested configuration objects (SKUs, network rules, etc.). |
| [`BicepValueOfT.cs`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/BicepValueOfT.cs) | Generic value container — can be a literal, a Bicep expression, or unset. |
| [`Infrastructure.cs`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/Infrastructure.cs) | Container holding resources, parameters, outputs, and variables. Builds a `ProvisioningPlan`. |
| [`ProvisioningPlan.cs`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/ProvisioningPlan.cs) | Result of `Infrastructure.Build()`. Can be saved to disk as `.bicep` files. |

### BicepValue Type System

Every property on a provisioning resource or construct is wrapped in one of three types:

| Type | Use Case | Example |
|------|----------|---------|
| `BicepValue<T>` | Single scalar value | `BicepValue<string> Name` |
| `BicepList<T>` | Ordered collection | `BicepList<SubnetData> Subnets` |
| `BicepDictionary<T>` | Key-value pairs | `BicepDictionary<string> Tags` |

Each wrapper can be in one of three states:

- **Literal** — a concrete .NET value (e.g. `"myaccount"`).
- **Expression** — a Bicep expression referencing another resource's output (e.g. `storage.name`).
- **Unset** — not yet assigned; accessing it will not throw.

This design allows safe cross-resource references at design time without knowing runtime values.

### Common Generated Resources

The core library also contains ~30 generated resource classes for ARM-level concepts:

`ResourceGroup`, `RoleAssignment`, `UserAssignedIdentity`, `ArmDeployment`, `PolicyAssignment`, `ManagementLock`, `TemplateSpec`, and more.

These are generated by the same generator that produces RP libraries (see below) with a set of baseline specifications.

---

## Resource Provider (RP) Libraries

Each Azure service gets its own `Azure.Provisioning.<Service>` NuGet package. There are currently **28 RP libraries**:

| Package | Service |
|---------|---------|
| `Azure.Provisioning.AppConfiguration` | App Configuration |
| `Azure.Provisioning.AppContainers` | Container Apps |
| `Azure.Provisioning.ApplicationInsights` | Application Insights |
| `Azure.Provisioning.AppService` | App Service / Functions |
| `Azure.Provisioning.CognitiveServices` | Cognitive Services / Azure AI |
| `Azure.Provisioning.Communication` | Communication Services |
| `Azure.Provisioning.ContainerRegistry` | Container Registry |
| `Azure.Provisioning.ContainerService` | Kubernetes Service (AKS) |
| `Azure.Provisioning.CosmosDB` | Cosmos DB |
| `Azure.Provisioning.Dns` | DNS |
| `Azure.Provisioning.EventGrid` | Event Grid |
| `Azure.Provisioning.EventHubs` | Event Hubs |
| `Azure.Provisioning.FrontDoor` | Front Door / CDN |
| `Azure.Provisioning.KeyVault` | Key Vault |
| `Azure.Provisioning.Kubernetes` | Arc-enabled Kubernetes |
| `Azure.Provisioning.KubernetesConfiguration` | Kubernetes Configuration |
| `Azure.Provisioning.Kusto` | Data Explorer (Kusto) |
| `Azure.Provisioning.Network` | Virtual Network, Load Balancer, etc. |
| `Azure.Provisioning.OperationalInsights` | Log Analytics |
| `Azure.Provisioning.PostgreSql` | PostgreSQL |
| `Azure.Provisioning.PrivateDns` | Private DNS |
| `Azure.Provisioning.Redis` | Azure Cache for Redis |
| `Azure.Provisioning.RedisEnterprise` | Redis Enterprise |
| `Azure.Provisioning.Search` | Azure AI Search |
| `Azure.Provisioning.ServiceBus` | Service Bus |
| `Azure.Provisioning.SignalR` | SignalR |
| `Azure.Provisioning.Sql` | SQL Database |
| `Azure.Provisioning.Storage` | Storage |
| `Azure.Provisioning.WebPubSub` | Web PubSub |

### Structure of an RP Library

Each RP library follows the same layout:

```
Azure.Provisioning.<Service>/
├── src/
│   ├── Azure.Provisioning.<Service>.csproj
│   ├── Generated/              # Auto-generated resource classes
│   │   ├── <Resource>.cs       # e.g. StorageAccount.cs
│   │   └── Models/             # Generated supporting model types
│   ├── BackwardCompatible/     # Partial class customizations (if needed)
│   └── <Service>BuiltInRole.cs # Generated RBAC role definitions
├── tests/
├── api/                        # Public API surface tracking
├── CHANGELOG.md
└── README.md
```

### Anatomy of a Generated Resource

Every generated resource class inherits from [`ProvisionableResource`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/src/Primitives/ProvisionableResource.cs) and follows the same pattern. For example, [`StorageAccount`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning.Storage/src/Generated/StorageAccount.cs):

```csharp
public partial class StorageAccount : ProvisionableResource
{
    // Input properties — settable by the user
    public BicepValue<string> Name
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }
    private BicepValue<string>? _name;

    public BicepValue<AzureLocation> Location { get; set; }
    public StorageSku Sku { get; set; }                         // Nested construct
    public BicepDictionary<string> Tags { get; set; }           // Dictionary property

    // Output properties — read-only, populated after deployment
    public BicepValue<ResourceIdentifier> Id { get; }
    public SystemData SystemData { get; }
}
```

Key patterns:

- **`BicepValue<T>`** wraps every property for Bicep expression support.
- **`partial class`** allows hand-authored backward-compatibility code alongside generated code.
- **Nested construct types** (e.g. `StorageSku`) inherit from `ProvisionableConstruct`.
- **Built-in RBAC roles** are generated as a `readonly struct` (e.g. `StorageBuiltInRole`) with constants for role GUIDs.

---

## Code Generation — Current Generator

**Source:** [`sdk/provisioning/Generator/`](https://github.com/Azure/azure-sdk-for-net/tree/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Generator)

The current generator is a standalone C# console application that produces the `Generated/` folder for every RP library. It does **not** use TypeSpec or AutoRest. Instead, it uses **reflection** on the corresponding [Azure Resource Manager .NET SDK](https://github.com/Azure/azure-sdk-for-net/tree/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/resourcemanager) assemblies.

For each service, the generator reads the management-plane SDK assembly (e.g. `Azure.ResourceManager.Storage`), discovers its resource types, models, and enums via reflection, and performs a type mapping to convert management-plane properties into provisioning-plane equivalents — wrapping them in `BicepValue<T>`, `BicepList<T>`, or `BicepDictionary<T>` as appropriate, and emitting classes that inherit from `ProvisionableResource` or `ProvisionableConstruct` instead of the ARM SDK base types.

Each service has a **Specification** class (e.g. [`StorageSpecification`](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Generator/src/Specifications/StorageSpecification.cs)) that can customize the mapping — removing irrelevant properties, renaming models, adding naming constraints, marking sensitive values, and defining built-in RBAC roles.

---

## Future Plans — TypeSpec-Based Generation

> **PR:** [Azure/azure-sdk-for-net#56627 — Introduce the Provisioning Generator (TypeSpec-based generation)](https://github.com/Azure/azure-sdk-for-net/pull/56627) \
> **Status:** In active development (draft)

### Motivation

The current generator reflects on already-published Azure Resource Manager .NET SDK assemblies, which means the provisioning library can only be updated *after* the management-plane SDK has been released. This creates a sequential dependency chain (TypeSpec → ARM SDK release → Provisioning library) and prevents the two libraries from sharing a single source of truth or releasing together.

The new generator will generate provisioning libraries **directly from TypeSpec ARM definitions** — the same definitions used to generate the management-plane SDK. This ensures that both the management-plane library and its corresponding provisioning library share the same source of truth and can be generated, validated, and released together as part of a single workflow.

### Where It Lives

The new generator will be located at:

```
eng/packages/http-client-csharp-provisioning/
├── emitter/        # TypeScript emitter (TypeSpec → intermediate representation)
├── generator/      # C# generator (intermediate representation → provisioning library code)
│   ├── Azure.Generator.Provisioning/
│   │   └── src/
│   └── TestProjects/
├── docs/           # Design documents
└── eng/scripts/    # Generation scripts
```

This mirrors the structure of the existing [management-plane generator](https://github.com/Azure/azure-sdk-for-net/tree/d4bf453010d6a3e2107884bee0132b98e78f8c36/eng/packages/http-client-csharp-mgmt) (`http-client-csharp-mgmt`).

### Architecture

The new generator extends the management-plane generator framework using **TypeFactory extension points**:

- **`ProvisioningTypeFactory`** — intercepts type creation to produce `BicepValue<T>`, `BicepList<T>`, and `BicepDictionary<T>` types instead of standard model types.
- **`ProvisioningModelProvider`** — generates classes that inherit from `ProvisionableConstruct`.
- **`ProvisioningResourceProvider`** — generates classes that inherit from `ProvisionableResource`, including ARM system properties (`Name`, `Location`, `Tags`, `Id`, `SystemData`), `FromExisting()` factory methods, and `ResourceVersions` constants.
- **`ProvisioningEnumProvider`** — generates simple C# enums (instead of the extensible structs used by management-plane SDKs).
- **`ProvisioningOutputLibrary`** — filters out management-plane artifacts (clients, serialization types) that are not relevant to provisioning.

---

## Additional References

- [Azure.Provisioning core library README](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/Azure.Provisioning/README.md)
- [Azure Provisioning overview README](https://github.com/Azure/azure-sdk-for-net/blob/d4bf453010d6a3e2107884bee0132b98e78f8c36/sdk/provisioning/README.md)
- [Azure Developer CLI — Getting started](https://learn.microsoft.com/azure/developer/azure-developer-cli/get-started?tabs=localinstall&pivots=programming-language-csharp)
- [Azure.Provisioning on NuGet](https://www.nuget.org/packages/Azure.Provisioning/)
