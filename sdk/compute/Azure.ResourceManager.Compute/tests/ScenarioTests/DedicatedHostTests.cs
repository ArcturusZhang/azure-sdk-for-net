// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.TestFramework;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Core;
using NUnit.Framework;

namespace Azure.ResourceManager.Compute.Tests
{
    public class DedicatedHostTests : VMTestBase
    {
        private string baseResourceGroupName;
        private string resourceGroupName;
        private ResourceGroup _resourceGroup;

        private DedicatedHostGroupContainer _dedicatedHostGroupContainer;

        public DedicatedHostTests(bool isAsync) : base(isAsync)
        {
            Environment.SetEnvironmentVariable("LOCATION", Location.EastUS2);
        }

        private async Task Initialize()
        {
            baseResourceGroupName = Recording.GenerateAssetName(TestPrefix);
            resourceGroupName = baseResourceGroupName + "_DH";

            _resourceGroup = (await ResourceGroupContainer.CreateOrUpdateAsync(
                resourceGroupName,
                new ResourceGroupData(TestEnvironment.Location)
                {
                    Tags = { { resourceGroupName, Recording.UtcNow.ToString("u") } }
                })).Value;
            _dedicatedHostGroupContainer = _resourceGroup.GetDedicatedHostGroups();
        }

        [RecordedTest]
        public async Task TestDedicatedHostOperations()
        {
            await Initialize();
            string dhgName = "DHG-1";
            string dhName = "DH-1";
            // Create a dedicated host group, then get the dedicated host group and validate that they match
            DedicatedHostGroup createdDHG = await _dedicatedHostGroupContainer.CreateOrUpdateAsync(
                dhgName,
                new DedicatedHostGroupData(TestEnvironment.Location)
                {
                    Zones = { "1" },
                    PlatformFaultDomainCount = 1
                });
            DedicatedHostGroup returnedDHG = await _dedicatedHostGroupContainer.GetAsync(dhgName);
            ValidateDedicatedHostGroup(createdDHG.Data, returnedDHG.Data);

            // TODO -- the Update method is not generated for dedicated host group
            //// Update existing dedicated host group
            //DedicatedHostGroupUpdate updateDHGInput = new DedicatedHostGroupUpdate()
            //{
            //    Tags = { { "testKey", "testValue" } }
            //};
            //createdDHG.Data.Tags.InitializeFrom(updateDHGInput.Tags);
            //updateDHGInput.PlatformFaultDomainCount = returnedDHG.Data.PlatformFaultDomainCount; // There is a bug in PATCH.  PlatformFaultDomainCount is a required property now.
            //returnedDHG = await createdDHG.UpdateAsync(rgName, dhgName, updateDHGInput);
            //ValidateDedicatedHostGroup(createdDHG, returnedDHG);

            // Update the tags for this dedicated host group
            var tags = new Dictionary<string, string>()
            {
                {"testKey", "testValue" }
            };
            createdDHG = await createdDHG.SetTagsAsync(tags);
            Assert.AreEqual(createdDHG.Data.Tags, tags);

            // List DedicatedHostGroups by resourceGroup
            var listDHGsResponse = _dedicatedHostGroupContainer.ListAsync();
            var listDHGsResponseRes = await listDHGsResponse.ToEnumerableAsync();
            Assert.IsTrue(listDHGsResponseRes.Count() == 1);
            ValidateDedicatedHostGroup(createdDHG.Data, listDHGsResponseRes.First().Data);

            // List DedicatedHostGroups by subscription
            listDHGsResponse = DefaultSubscription.ListDedicatedHostGroupsAsync();
            listDHGsResponseRes = await listDHGsResponse.ToEnumerableAsync();
            // There might be multiple dedicated host groups in the subscription, we only care about the one that we created.
            returnedDHG = listDHGsResponseRes.First(dhg => dhg.Id == createdDHG.Id);
            Assert.NotNull(returnedDHG);
            ValidateDedicatedHostGroup(createdDHG.Data, returnedDHG.Data);

            // Create DedicatedHost within the DedicatedHostGroup and validate
            var dedicatedHostContainer = createdDHG.GetDedicatedHosts();
            DedicatedHost createdDH = await dedicatedHostContainer.CreateOrUpdateAsync(dhName,
                new DedicatedHostData(TestEnvironment.Location, new Models.Sku() { Name = "ESv3-Type1" })
                {
                    Tags = { { baseResourceGroupName, Recording.UtcNow.ToString("u") } }
                });
            DedicatedHost returnedDH = await dedicatedHostContainer.GetAsync(dhName);
            ValidateDedicatedHost(createdDH.Data, returnedDH.Data);

            // List DedicatedHosts by host groups
            var listDHsResponse = dedicatedHostContainer.ListAsync();
            var listDHsResponseRes = await listDHsResponse.ToEnumerableAsync();
            Assert.IsTrue(listDHsResponseRes.Count() == 1);
            ValidateDedicatedHost(createdDH.Data, listDHsResponseRes.First().Data);

            // Delete DedicatedHosts and DedicatedHostGroups
            await createdDH.DeleteAsync();

            // TODO -- there is a bug in the API, despite the GET of dedicated host returns 404, we still cannot delete the dedicated host group
            //await createdDHG.DeleteAsync();
        }

        private void ValidateDedicatedHostGroup(DedicatedHostGroupData expectedDHG, DedicatedHostGroupData actualDHG)
        {
            if (expectedDHG == null)
            {
                Assert.Null(actualDHG);
            }
            else
            {
                Assert.NotNull(actualDHG);
                if (expectedDHG.Hosts == null)
                {
                    Assert.Null(actualDHG.Hosts);
                }
                else
                {
                    Assert.NotNull(actualDHG);
                    Assert.True(actualDHG.Hosts.SequenceEqual(expectedDHG.Hosts));
                }
                Assert.AreEqual(expectedDHG.Location, actualDHG.Location);
                Assert.AreEqual(expectedDHG.Name, actualDHG.Name);
            }
        }

        private void ValidateDedicatedHost(DedicatedHostData expectedDH, DedicatedHostData actualDH)
        {
            if (expectedDH == null)
            {
                Assert.Null(actualDH);
            }
            else
            {
                Assert.NotNull(actualDH);
                if (expectedDH.VirtualMachines == null)
                {
                    Assert.Null(actualDH.VirtualMachines);
                }
                else
                {
                    Assert.NotNull(actualDH);
                    Assert.True(actualDH.VirtualMachines.SequenceEqual(expectedDH.VirtualMachines));
                }
                Assert.AreEqual(expectedDH.Location, actualDH.Location);
                Assert.AreEqual(expectedDH.Name, actualDH.Name);
                Assert.AreEqual(expectedDH.HostId, actualDH.HostId);
            }
        }

        protected async Task<DedicatedHost> CreateDedicatedHost(DedicatedHostGroup dedicatedHostGroup, string dedicatedHostName)
        {
            return await dedicatedHostGroup.GetDedicatedHosts().CreateOrUpdateAsync(dedicatedHostName,
                new DedicatedHostData(TestEnvironment.Location, new Models.Sku() { Name = "ESv3-Type1" })
                {
                    Tags = { { baseResourceGroupName, Recording.UtcNow.ToString("u") } }
                });
        }
    }
}
