// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.TestFramework;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute.Tests.Helpers;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using NUnit.Framework;
using static Azure.ResourceManager.Compute.Tests.Helpers.CloudServiceConfiguration;
using static Azure.ResourceManager.Compute.Tests.Helpers.CloudServiceConfigurationHelper;

namespace Azure.ResourceManager.Compute.Tests
{
    public class CloudServiceCollectionTests : ComputeTestBase
    {
        public CloudServiceCollectionTests(bool isAsync)
            : base(isAsync, RecordedTestMode.Record)
        {
        }

        private async Task<CloudServiceCollection> GetCloudServiceCollectionAsync()
        {
            var resourceGroup = await CreateResourceGroupAsync();
            return resourceGroup.GetCloudServices();
        }

        [TestCase]
        [RecordedTest]
        public async Task CreateOrUpdate()
        {
            //var collection = await GetCloudServiceCollectionAsync();
            //var setName = Recording.GenerateAssetName("testCS-");
            //var input = ResourceDataHelper.GetBasicAvailabilitySetData(DefaultLocation);
            //input.Tags.ReplaceWith(new Dictionary<string, string>
            //{
            //    { "key", "value" }
            //});
            //var lro = await collection.CreateOrUpdateAsync(WaitUntil.Completed, setName, input);
            //var availabilitySet = lro.Value;
            //Assert.AreEqual(setName, availabilitySet.Data.Name);

            var csName = Recording.GenerateAssetName("testcs");
            var cloudServiceName = "TestCloudServiceMultiRole";
            var publicIPAddressName = Recording.GenerateAssetName("cspip");
            var vnetName = Recording.GenerateAssetName("csvnet");
            var subnetName = Recording.GenerateAssetName("subnet");
            var dnsName = Recording.GenerateAssetName("dns");
            var lbName = Recording.GenerateAssetName("lb");
            var lbfeName = Recording.GenerateAssetName("lbfe");

            var resourceGroup = await CreateResourceGroupAsync();

            // create virtual network
            var vnetData = new VirtualNetworkData()
            {
                AddressPrefixes = { "10.0.0.0/16" },
                Subnets =
                {
                    new SubnetData()
                    {
                        Name = subnetName,
                        AddressPrefix = "10.0.0.0/24"
                    }
                },
                Location = DefaultLocation
            };
            var vnet = (await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetData)).Value;
            // create public ip address
            var publicIPData = new PublicIPAddressData()
            {
                Location = DefaultLocation,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
                DnsSettings = new PublicIPAddressDnsSettings()
                {
                    DomainNameLabel = dnsName
                }
            };
            var publicIP = (await resourceGroup.GetPublicIPAddresses().CreateOrUpdateAsync(WaitUntil.Completed, publicIPAddressName, publicIPData)).Value;

            ///
            /// Create: Create a multi-role CloudService with 2 WorkerRoles and 1 WebRole
            ///

            // Define Configurations
            var supportedRoleInstanceSizes = GetSupportedRoleInstanceSizes();
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping = new Dictionary<string, RoleConfiguration>
            {
                { "WorkerRole1", new RoleConfiguration { InstanceCount = 1, RoleInstanceSize = supportedRoleInstanceSizes[0] } },
                { "WorkerRole2", new RoleConfiguration { InstanceCount = 1, RoleInstanceSize = supportedRoleInstanceSizes[1] } },
                { "WebRole1", new RoleConfiguration { InstanceCount = 2, RoleInstanceSize = supportedRoleInstanceSizes[3] } }
            };

            // Generate the request
            CloudServiceData cloudServiceData = GenerateCloudServiceWithNetworkProfile(
                serviceName: cloudServiceName,
                cspkgSasUri: CreateCspkgSasUrl(rgName, MultiRole2Worker1WebRolesPackageSasUri),
                roleNameToPropertiesMapping: roleNameToPropertiesMapping,
                vnetName: vnetName,
                subnetName: subnetName,
                lbName: lbName,
                lbFrontendName: lbfeName);

            CloudService getResponse = CreateCloudService_NoAsyncTracking(
                rgName,
                csName,
                cloudService);
        }

        protected CloudServiceData GenerateCloudServiceWithNetworkProfile(string serviceName, string cspkgSasUri, string vnetName, string subnetName, string lbName, string lbFrontendName, Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping, ResourceIdentifier publicIPAddressId)
        {
            CloudServiceData cloudService = GenerateCloudService(serviceName, cspkgSasUri, vnetName, subnetName, roleNameToPropertiesMapping);
            cloudService.NetworkProfile = GenerateNrpCloudServiceNetworkProfile(publicIPAddressId, lbName, lbFrontendName);
            return cloudService;
        }

        protected CloudServiceData GenerateCloudService(string serviceName,
            string cspkgSasUri,
            string vnetName,
            string subnetName,
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping,
            List<ServiceConfigurationRoleCertificate> cscfgCerts = null,
            ServiceConfigurationRoleSecurityConfigurations securityConfigurations = null,
            CloudServiceVaultSecretGroup vaultGroup = null)
        {
            CloudServiceData cloudService = new CloudServiceData(DefaultLocation)
            {
                Configuration = GenerateBase64EncodedCscfgWithNetworkConfiguration(serviceName, roleNameToPropertiesMapping, vnetName, subnetName, null, cscfgCerts, securityConfigurations),
                PackageUri = new Uri(cspkgSasUri),
            };
            foreach (var item in GenerateRoles(roleNameToPropertiesMapping))
            {
                cloudService.Roles.Add(item);
            }
            if (vaultGroup != null)
            {
                cloudService.OSProfile = new CloudServiceOSProfile()
                {
                    Secrets =
                    {
                        vaultGroup
                    }
                };
            }
            return cloudService;
        }

        protected CloudServiceNetworkProfile GenerateNrpCloudServiceNetworkProfile(ResourceIdentifier publicIPAddressId, string lbName, string lbFrontEndName)
        {
            var feipConfig = new LoadBalancerFrontendIPConfiguration(lbFrontEndName)
            {
                PublicIPAddressId = publicIPAddressId,
            };
            var cloudServiceNetworkProfile = new CloudServiceNetworkProfile()
            {
                LoadBalancerConfigurations =
                {
                    new LoadBalancerConfiguration(lbName, new[] {feipConfig}),
                }
            };

            return cloudServiceNetworkProfile;
        }

        protected static IEnumerable<CloudServiceRoleProfileProperties> GenerateRoles(Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping)
        {
            foreach (var roleName in roleNameToPropertiesMapping.Keys)
            {
                yield return new CloudServiceRoleProfileProperties()
                {
                    Name = roleName,
                    Sku = new CloudServiceRoleSku
                    {
                        Name = roleNameToPropertiesMapping[roleName].RoleInstanceSize,
                        Capacity = roleNameToPropertiesMapping[roleName].InstanceCount,
                        Tier = roleNameToPropertiesMapping[roleName].RoleInstanceSize.IndexOf("_", StringComparison.InvariantCulture) != -1 ? roleNameToPropertiesMapping[roleName].RoleInstanceSize.Substring(0, roleNameToPropertiesMapping[roleName].RoleInstanceSize.IndexOf("_")) : null
                    }
                };
            }
        }

        protected string CreateCspkgSasUrl(string rgName, string fileName)
        {
            string storageAccountName = Recording.GenerateAssetName("saforcspkg");
            string asName = Recording.GenerateAssetName("asforcspkg");
            StorageAccount storageAccountOutput = CreateStorageAccount(rgName, storageAccountName); // resource group is also created in this method.
            string applicationMediaLink = @"https://saforcspkg1969.blob.core.windows.net/sascontainer/" + fileName;
            if (Mode == RecordedTestMode.Record)
            {
                var accountKeyResult = m_SrpClient.StorageAccounts.ListKeysWithHttpMessagesAsync(rgName, storageAccountName).Result;
                CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(storageAccountName, accountKeyResult.Body.Key1), useHttps: true);

                var blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference("sascontainer");
                container.CreateIfNotExistsAsync().Wait();

                CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileName);
                blockBlob.UploadFromFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName)).Wait();

                SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
                sasConstraints.SharedAccessStartTime = DateTime.UtcNow.AddDays(-1);
                sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(2);
                sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;

                //Generate the shared access signature on the blob, setting the constraints directly on the signature.
                string sasContainerToken = blockBlob.GetSharedAccessSignature(sasConstraints);

                //Return the URI string for the container, including the SAS token.
                applicationMediaLink = blockBlob.Uri + sasContainerToken;
            }
            return applicationMediaLink;
        }

        /// <summary>
        /// Returns a List of supported RoleInstance Sizes based on the environment.
        /// Note: The ordering of the List is important as all tests will have size dependency in their CSPKG.
        /// By Default most tests have dependency on ""Standard_D2_v2" for Prod regions.
        /// </summary>
        internal static List<string> GetSupportedRoleInstanceSizes()
        {
            return new List<string> { "Standard_D2_v2", "Standard_D1_v2", "Standard_A1", "Standard_A2_v2" };
        }

        protected static string GenerateBase64EncodedCscfgWithNetworkConfiguration(string serviceName,
            Dictionary<string, RoleConfiguration> roleNameToPropertiesMapping,
            string vNetName,
            string subnetName,
            ServiceConfigurationNetworkConfigurationAddressAssignmentsReservedIPs reservedIPs = null,
            List<ServiceConfigurationRoleCertificate> cscfgCerts = null,
            ServiceConfigurationRoleSecurityConfigurations securityConfigurations = null,
            int osFamily = 5,
            Setting[] serviceSettings = null) => CloudServiceConfigurationHelper.GenerateServiceConfiguration(
                serviceName: serviceName,
                osFamily: osFamily,
                osVersion: "*",
                roleNameToPropertiesMapping: roleNameToPropertiesMapping,
                schemaVersion: "2015-04.2.6",
                vNetName: vNetName,
                subnetName: subnetName,
                reservedIPs: reservedIPs,
                certificates: cscfgCerts,
                securityConfigurations: securityConfigurations,
                serviceSettings: serviceSettings
                );

        //[TestCase]
        //[RecordedTest]
        //public async Task Get()
        //{
        //    var collection = await GetAvailabilitySetCollectionAsync();
        //    var setName = Recording.GenerateAssetName("testAS-");
        //    var input = ResourceDataHelper.GetBasicAvailabilitySetData(DefaultLocation);
        //    input.Tags.ReplaceWith(new Dictionary<string, string>
        //    {
        //        { "key", "value" }
        //    });
        //    var lro = await collection.CreateOrUpdateAsync(WaitUntil.Completed, setName, input);
        //    AvailabilitySetResource set1 = lro.Value;
        //    AvailabilitySetResource set2 = await collection.GetAsync(setName);

        //    ResourceDataHelper.AssertAvailabilitySet(set1.Data, set2.Data);
        //}

        //[TestCase]
        //[RecordedTest]
        //public async Task Exists()
        //{
        //    var collection = await GetAvailabilitySetCollectionAsync();
        //    var setName = Recording.GenerateAssetName("testAS-");
        //    var input = ResourceDataHelper.GetBasicAvailabilitySetData(DefaultLocation);
        //    input.Tags.ReplaceWith(new Dictionary<string, string>
        //    {
        //        { "key", "value" }
        //    });
        //    var lro = await collection.CreateOrUpdateAsync(WaitUntil.Completed, setName, input);
        //    var availabilitySet = lro.Value;
        //    Assert.IsTrue(await collection.ExistsAsync(setName));
        //    Assert.IsFalse(await collection.ExistsAsync(setName + "1"));

        //    Assert.ThrowsAsync<ArgumentNullException>(async () => _ = await collection.ExistsAsync(null));
        //}

        //[TestCase]
        //[RecordedTest]
        //public async Task GetAll()
        //{
        //    var collection = await GetAvailabilitySetCollectionAsync();
        //    var input = ResourceDataHelper.GetBasicAvailabilitySetData(DefaultLocation);
        //    input.Tags.ReplaceWith(new Dictionary<string, string>
        //    {
        //        { "key", "value" }
        //    });
        //    _ = await collection.CreateOrUpdateAsync(WaitUntil.Completed, Recording.GenerateAssetName("testAS-"), input);
        //    _ = await collection.CreateOrUpdateAsync(WaitUntil.Completed, Recording.GenerateAssetName("testAs-"), input);
        //    int count = 0;
        //    await foreach (var availabilitySet in collection.GetAllAsync())
        //    {
        //        count++;
        //    }
        //    Assert.GreaterOrEqual(count, 2);
        //}

        //[TestCase]
        //[RecordedTest]
        //public async Task GetAllInSubscription()
        //{
        //    var collection = await GetAvailabilitySetCollectionAsync();
        //    var setName1 = Recording.GenerateAssetName("testAS-");
        //    var setName2 = Recording.GenerateAssetName("testAS-");
        //    var input = ResourceDataHelper.GetBasicAvailabilitySetData(DefaultLocation);
        //    input.Tags.ReplaceWith(new Dictionary<string, string>
        //    {
        //        { "key", "value" }
        //    });
        //    _ = await collection.CreateOrUpdateAsync(WaitUntil.Completed, setName1, input);
        //    _ = await collection.CreateOrUpdateAsync(WaitUntil.Completed, setName2, input);

        //    AvailabilitySetResource set1 = null, set2 = null;
        //    await foreach (var availabilitySet in DefaultSubscription.GetAvailabilitySetsAsync())
        //    {
        //        if (availabilitySet.Data.Name == setName1)
        //            set1 = availabilitySet;
        //        if (availabilitySet.Data.Name == setName2)
        //            set2 = availabilitySet;
        //    }

        //    Assert.NotNull(set1);
        //    Assert.NotNull(set2);
        //}
    }
}
