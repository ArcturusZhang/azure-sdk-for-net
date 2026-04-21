# PR #58290 — Flatten value-type nullable overhaul: API impact audit

Branch: `fix/mgmt-generator-flatten-value-type-nullable` · Generator change: `FlattenPropertyVisitor.PropertyFlatten` lift-to-nullable predicate.

## The fix in one sentence

When a model `M` is *flattened* into a parent (typical case: `Resource.properties` of type `M`), the generator now correctly lifts a **required value-type member** of `M` to `Nullable<T>` on the parent **iff the wrapper is optional** (so the inner value may not actually be present at runtime), and conversely keeps it non-nullable when the wrapper is required.

Two surface-level shapes of API change therefore appear:

| Direction | What it means | Triggered when |
|---|---|---|
| **`T → T?`** (lift-to-nullable) | Property became nullable on the public API | Wrapper is *optional* (`properties?` / no `, false`) **and** inner field is *required* + value-type |
| **`T? → T`** (de-nullify) | Property/parameter is no longer nullable | Wrapper is *required* (`<Props, false>`) **and** inner field is *required* + value-type — old generator over-nullified |

Both directions are corrections of pre-existing bugs.

## Verification methodology

For each of the 15 packages with `api/*.cs` diffs, I (1) extracted the impacted property/parameter and direction from `git diff`, (2) located the source field in the TypeSpec spec at the commit pinned in `tsp-location.yaml`, and (3) checked the chain: outer resource template, wrapper optionality, inner field optionality and type.

Spec repo path used for inspection: `C:\Users\dapzhang\Documents\workspace\azure-rest-api-specs`.

---

## Verdict summary

| # | Package | Verdict |
|---|---|---|
| 1 | ArtifactSigning | ✅ matches pattern (T→T?) |
| 2 | BillingBenefits | ✅ matches pattern — broader scope (multiple sub-models) |
| 3 | CertificateRegistration | ✅ matches pattern (T→T?) |
| 4 | ContainerService | ✅ matches pattern (T?→T de-nullify) |
| 5 | CosmosDBForPostgreSql | ✅ matches pattern — *nested* flatten via `RolePropertiesExternalIdentity` |
| 6 | DesktopVirtualization | ✅ matches pattern (T?→T de-nullify) |
| 7 | ElasticSan | ✅ matches pattern (T?→T de-nullify) |
| 8 | ContainerServiceFleet | ✅ matches pattern (T→T?) — `Gate = ProxyResource<GateProperties>` is optional-parent |
| 9 | NetworkCloud | ✅ matches pattern (T?→T de-nullify, multiple resources) |
| 10 | Resources.Policy | ✅ matches pattern (T→T?) |
| 11 | Storage | ✅ matches pattern (T?→T de-nullify) |
| 12 | StorageMover | ✅ matches pattern (T?→T de-nullify) |
| 13 | Support | ✅ matches pattern (T?→T de-nullify) |
| 14 | TrustedSigning | ✅ matches pattern (T→T?) |
| 15 | WorkloadsSapVirtualInstance | ✅ matches pattern (T→T?) |

**All 15 packages match the targeted pattern.** No unrelated regressions were found.

---

## Per-package details

### 1. ArtifactSigning — ✅ T → T?

| Impacted property | `CertificateProfile.ProfileType` (enum `CertificateProfileType`) |
|---|---|
| TSP property | `profileType: ProfileType;` (required) |
| TSP file | `models.tsp` |
| Spec link | [models.tsp#L173-L175](https://github.com/Azure/azure-rest-api-specs/blob/85d2bcc47b5b93c131e4255d70de91f16cf47282/specification/codesigning/CodeSigning.Management/models.tsp#L173-L175) |
| Resource template | `CertificateProfile is ProxyResource<CertificateProfileProperties>` (default-optional wrapper) |
| Wrapper optionality | `properties?` (default) |
| Inner field optionality | required |

### 2. BillingBenefits — ✅ broader: multiple optional sub-models with required value-type fields

| # | Property | Change |
|---|---|---|
| a | `…EntityType` (enum `MaccEntityType`) on `ApplicableMacc` & related | T → T? |
| b | `Location` (`AzureLocation` struct) on `ApplicableMacc`, `BillingBenefitsReservationOrderAliasData`, `…CreateOrUpdateContent`, `CreditsValidateModel` | T → T? |
| c | `ResourceType` (`ResourceType` struct) on those same models | T → T? |

Each of these models contains a required value-type member that lives inside an *optional* sub-model wrapper. The lift is correct.

| TSP entry point | [Macc.tsp](https://github.com/Azure/azure-rest-api-specs/blob/1cb81ab20c654ce193bcf5afa4462b99cc452cc6/specification/billingbenefits/BillingBenefits.Management/Macc.tsp), [models.tsp#L2229-L2246](https://github.com/Azure/azure-rest-api-specs/blob/1cb81ab20c654ce193bcf5afa4462b99cc452cc6/specification/billingbenefits/BillingBenefits.Management/models.tsp#L2229-L2246) |
|---|---|
| Resource template | `Macc is TrackedResource<MaccModelProperties>` (default-optional wrapper) |

> Note: `Location` and `ResourceType` are `Azure.Core` *struct* value types, so they fit the same lift rule as enums. The same fix logic naturally extends to them.

### 3. CertificateRegistration — ✅ T → T?

| Impacted property | `AppServiceCertificateOrder.ProductType` (enum `CertificateProductType`) |
|---|---|
| TSP property | `productType: CertificateProductType;` (required) |
| Spec link | [models.tsp#L166-L202](https://github.com/Azure/azure-rest-api-specs/blob/3a8cc999dcbcbb4239b11c470614fb343e0157a8/specification/certificateregistration/resource-manager/Microsoft.CertificateRegistration/CertificateRegistration/models.tsp#L166-L202) |
| Resource template | `AppServiceCertificateOrder is TrackedResource<AppServiceCertificateOrderProperties>` (default-optional wrapper) |

### 4. ContainerService — ✅ T? → T (de-nullify)

| Impacted parameter | `osType` (enum `ContainerServiceOSType`) on `AgentPoolUpgradeProfileData` model factory |
|---|---|
| TSP property | `osType: OSType;` (required) |
| Spec link | [AgentPoolUpgradeProfile.tsp#L58-L67](https://github.com/Azure/azure-rest-api-specs/blob/b684aff3319ffa5784a8b8d19d6af5adf168a4bb/specification/containerservice/resource-manager/Microsoft.ContainerService/aks/AgentPoolUpgradeProfile.tsp#L58-L67) |
| Resource template | `AgentPoolUpgradeProfile is ProxyResource<AgentPoolUpgradeProfileProperties, false>` (**required** wrapper) |

Required wrapper + required inner ⇒ no lift needed. Old generator was incorrectly nullifying; new generator correctly leaves it non-nullable.

### 5. CosmosDBForPostgreSql — ✅ T → T? via *nested* flatten

| Impacted property | `CosmosDBForPostgreSqlRoleData.PrincipalType` (enum `PrincipalType`) |
|---|---|
| TSP property | `principalType: PrincipalType;` (required) — but on `RolePropertiesExternalIdentity`, **not** directly on `RoleProperties` |
| Spec link | [models.tsp#L158-L159](https://github.com/Azure/azure-rest-api-specs/blob/41aa2e9f20cb7d28653078638d143bb0272658a3/specification/postgresqlhsc/resource-manager/Microsoft.DBforPostgreSQL/PostgresqlHsc/models.tsp#L158-L159) |
| Resource template | `Role is ProxyResource<RoleProperties, false>` |
| Flatten chain | `Role` → `properties` (required, `RoleProperties`) → `externalIdentity?` (**optional**, `RolePropertiesExternalIdentity`) → `principalType` (required, value-type) |

Although `RoleProperties` itself is required on the resource, `externalIdentity` *inside* it is optional. The lift propagates to the outermost flattened surface — correct.

### 6. DesktopVirtualization — ✅ T? → T (de-nullify)

| Impacted parameter | `applicationGroupType` (enum `VirtualApplicationGroupType`) on `VirtualApplicationGroupData` model factory |
|---|---|
| TSP property | `applicationGroupType: ApplicationGroupType;` (required) |
| Spec link | [models.tsp#L2288-L2319](https://github.com/Azure/azure-rest-api-specs/blob/8fa1530f0d493fd69670958d511489f919cd3392/specification/desktopvirtualization/resource-manager/Microsoft.DesktopVirtualization/DesktopVirtualization/models.tsp#L2288-L2319) |
| Resource template | `ApplicationGroup is TrackedResource<ApplicationGroupProperties, false>` (**required** wrapper) |

### 7. ElasticSan — ✅ T? → T (de-nullify)

| Impacted parameters | `baseSizeTiB` (`int64`), `extendedCapacitySizeTiB` (`int64`) on `ElasticSanData` model factory |
|---|---|
| Spec link | [models.tsp#L328-L355](https://github.com/Azure/azure-rest-api-specs/blob/f1ff11e47a77ce542d25c0e68e68d8cd5f498e00/specification/elasticsan/resource-manager/Microsoft.ElasticSan/ElasticSan/models.tsp#L328-L355) |
| Resource template | `ElasticSan is TrackedResource<ElasticSanProperties, false>` (**required** wrapper) |

### 8. ContainerServiceFleet — ✅ T → T?

| Impacted properties | `ContainerServiceFleetManagedNamespaceData.GateType`, `.State` (enums) |
|---|---|
| Actual TSP container | `GateProperties.gateType` and `GateProperties.state` (required) |
| TSP files | [gate.tsp#L56-L81](https://github.com/Azure/azure-rest-api-specs/blob/2ea4fc50ce3106d9f01946ad0026411cc967110f/specification/containerservice/resource-manager/Microsoft.ContainerService/fleet/gate.tsp#L56-L81), [fleetnamespace.tsp#L46-L50](https://github.com/Azure/azure-rest-api-specs/blob/2ea4fc50ce3106d9f01946ad0026411cc967110f/specification/containerservice/resource-manager/Microsoft.ContainerService/fleet/fleetnamespace.tsp#L46-L50) |
| Resource template | `Gate is ProxyResource<GateProperties>` (**default-optional** wrapper) |

> The `DeletePolicy → AdoptionPolicy` rename observed in the same diff is a pre-existing spec rename, **unrelated** to this fix.

### 9. NetworkCloud — ✅ T? → T across multiple resources

| Impacted parameters | `rackSlot` (`int64`), `clusterType` (enum `ClusterType`), `vlan` (`int64`), `cpuCores` (`int64`), `memorySizeGb` (`int64`) |
|---|---|
| Models | `BareMetalMachineConfigurationData`, `ClusterSpec`, `L3NetworkSpec`, `StorageApplianceSpec`, `VirtualMachineSpec` |
| Spec links | `Cluster_models.tsp#L429`, `BareMetalMachine_models.tsp#L672`, `StorageAppliance_models.tsp#L150`, `VirtualMachine_models.tsp#L497` & `L512`, `L3Network_models.tsp#L184` (all under `specification/networkcloud/NetworkCloud.Management/` at commit [`a7f81521…`](https://github.com/Azure/azure-rest-api-specs/tree/a7f8152138adccdbf476061fbebd9d6f70bedd1d/specification/networkcloud/NetworkCloud.Management)) |
| Resource templates | All `TrackedResource<…, false>` (**required** wrappers) |

### 10. Resources.Policy — ✅ T → T?

| Impacted property | `PolicyExemptionData.ExemptionCategory` (enum `PolicyExemptionCategory`) |
|---|---|
| TSP property | `exemptionCategory: ExemptionCategory;` (required) |
| Spec link | [models.tsp#L1320-L1334](https://github.com/Azure/azure-rest-api-specs/blob/999f565e72b17245eb638ce0fd9908d2aad2a0ec/specification/resources/resource-manager/Microsoft.Authorization/policy/models.tsp#L1320-L1334) |
| Resource template | `PolicyExemption is ExtensionResource<PolicyExemptionProperties>` (default-optional wrapper) |

### 11. Storage — ✅ T? → T (de-nullify)

| Impacted parameter | `targetSkuName` (enum `SkuName` → SDK `StorageSkuName`) on `StorageAccountMigrationData` model factory |
|---|---|
| TSP property | `targetSkuName: SkuName;` (required) |
| Spec link | [models.tsp#L4801-L4805](https://github.com/Azure/azure-rest-api-specs/blob/e43e4b3064cc42bf46f19785f2322ec076fd971a/specification/storage/Storage.Management/models.tsp#L4801-L4805) |
| Resource template | `StorageAccountMigration is ProxyResource<StorageAccountMigrationProperties, false>` (**required** wrapper) |

### 12. StorageMover — ✅ T? → T (de-nullify)

| Impacted parameter | `copyMode` (enum `CopyMode` → SDK `StorageMoverCopyMode`) on `JobDefinitionData` model factory |
|---|---|
| TSP property | `copyMode: CopyMode;` (required) |
| Spec link | [models.tsp#L692-L707](https://github.com/Azure/azure-rest-api-specs/blob/9e42299bb759f720eb4751496d5b13df26d87302/specification/storagemover/StorageMover.Management/models.tsp#L692-L707) |
| Resource template | `JobDefinition is ProxyResource<JobDefinitionProperties, false>` (**required** wrapper) |

### 13. Support — ✅ T? → T (de-nullify)

| Impacted parameters | `severity` (enum `SeverityLevel`), `advancedDiagnosticConsent` (enum `Consent`) on `SupportTicketData` model factory |
|---|---|
| TSP properties | both required on `SupportTicketDetailsProperties` |
| Spec link | [models.tsp#L470-L510](https://github.com/Azure/azure-rest-api-specs/blob/cfbf576abb3eb339c167f959e046e8c7f4efdcae/specification/support/resource-manager/Microsoft.Support/Support/models.tsp#L470-L510) |
| Resource template | `SupportTicketDetails is ProxyResource<SupportTicketDetailsProperties, false>` (**required** wrapper) |

### 14. TrustedSigning — ✅ T → T?

Same code shape as ArtifactSigning (different commit on the same `CodeSigning.Management` spec).

| Impacted property | `CertificateProfile.ProfileType` (enum `CertificateProfileType`) |
|---|---|
| Spec link | [models.tsp#L173-L175](https://github.com/Azure/azure-rest-api-specs/blob/1e7684349abdacee94cbf89200f319cd49e323f2/specification/codesigning/CodeSigning.Management/models.tsp#L173-L175) |
| Resource template | `CertificateProfile is ProxyResource<CertificateProfileProperties>` (default-optional wrapper) |

### 15. WorkloadsSapVirtualInstance — ✅ T → T?

| Impacted properties | `SapVirtualInstanceData.Environment` (enum `SAPEnvironmentType`), `.SapProduct` (enum `SAPProductType`) |
|---|---|
| TSP properties | both required on `SAPVirtualInstanceProperties` |
| Spec link | [models.tsp#L858-L869](https://github.com/Azure/azure-rest-api-specs/blob/241a61e926ba37f56654e85b117d4e32ec4c1bd5/specification/workloads/Workloads.SAPVirtualInstance.Management/models.tsp#L858-L869) |
| Resource template | `SAPVirtualInstance is TrackedResource<SAPVirtualInstanceProperties>` (default-optional wrapper) |

---

## Conclusion

All 15 packages with API surface changes match the intended fix pattern in both directions (lift-to-nullable for optional wrapper + required value-type inner; de-nullify for required wrapper + required value-type inner). No unrelated regressions surfaced from the export sweep.
