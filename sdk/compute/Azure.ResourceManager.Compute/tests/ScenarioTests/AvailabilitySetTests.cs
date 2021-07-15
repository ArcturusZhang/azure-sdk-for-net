// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.TestFramework;
using NUnit.Framework;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Core;

namespace Azure.ResourceManager.Compute.Tests
{
    public class AvailabilitySetTests : ComputeTestBase
    {
        private ResourceGroup _resourceGroup1;
        private ResourceGroup _resourceGroup2;
        private AvailabilitySetContainer _availabilitySetContainer;

        public string baseResourceGroupName;
        public string resourceGroup1Name;

        // These values are configurable in the service, but normal default values are FD = 3 and UD = 5
        // FD values can be 2 or 3
        // UD values are 1 -> 20
        public const int nonDefaultFD = 2;
        public const int nonDefaultUD = 4;

        public const int defaultFD = 3;
        public const int defaultUD = 5;

        // These constants for for the out of range tests
        public const int FDTooLow = 0;
        public const int FDTooHi = 4;
        public const int UDTooLow = 0;
        public const int UDTooHi = 21;

        public AvailabilitySetTests(bool isAsync) : base(isAsync)
        {
            Environment.SetEnvironmentVariable("LOCATION", Location.WestCentralUS);
        }

        [RecordedTest]
        public async Task TestAvailabilitySetOperations()
        {
            await Initialize();
            // Attempt to Create Availability Set with out of bounds FD and UD values
            await VerifyInvalidFDUDValuesFail();

            // Create a Availability Set with default values
            await VerifyDefaultValuesSucceed();

            // Make sure non default FD and UD values succeed
            await VerifyNonDefaultValuesSucceed();

            // Updating an Availability Set should fail
            //VerifyUpdateFails();

            // Make sure availability sets across resource groups are listed successfully
            await VerifyListAvailabilitySetsInSubscription();
        }

        private async Task VerifyInvalidFDUDValuesFail()
        {
            var inputAvailabilitySetName = Recording.GenerateAssetName("invalidfdud");
            var inputAvailabilitySet = new AvailabilitySetData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG", "rg"},
                        {"testTag", "1"},
                    },
            };

            // function to test the limits available.
            inputAvailabilitySet.PlatformFaultDomainCount = FDTooLow;
            AvailabilitySet createOrUpdateResponse = null;
            try
            {
                createOrUpdateResponse = await _availabilitySetContainer.CreateOrUpdateAsync(
                    inputAvailabilitySetName,
                    inputAvailabilitySet);
            }
            catch (Exception ex)
            //catch (CloudException ex)
            {
                Assert.NotNull(ex);
                //Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest);
            }
            Assert.True(createOrUpdateResponse == null);

            inputAvailabilitySet.PlatformFaultDomainCount = FDTooHi;
            try
            {
                createOrUpdateResponse = await _availabilitySetContainer.CreateOrUpdateAsync(
                    inputAvailabilitySetName,
                    inputAvailabilitySet);
            }
            catch (Exception ex)
            {
                Assert.NotNull(ex);
                //Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest);
            }
            Assert.True(createOrUpdateResponse == null);

            inputAvailabilitySet.PlatformUpdateDomainCount = UDTooLow;
            try
            {
                createOrUpdateResponse = await _availabilitySetContainer.CreateOrUpdateAsync(inputAvailabilitySetName, inputAvailabilitySet);
            }
            catch (Exception ex)
            {
                Assert.NotNull(ex);
                //Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest);
            }
            Assert.True(createOrUpdateResponse == null);

            inputAvailabilitySet.PlatformUpdateDomainCount = UDTooHi;
            try
            {
                createOrUpdateResponse = await _availabilitySetContainer.CreateOrUpdateAsync(inputAvailabilitySetName, inputAvailabilitySet);
            }
            catch (Exception ex)
            {
                Assert.NotNull(ex);
                //Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest);
            }
            Assert.True(createOrUpdateResponse == null);
        }

        private async Task VerifyDefaultValuesSucceed()
        {
            var inputAvailabilitySetName = Recording.GenerateAssetName("asdefaultvalues");
            var inputAvailabilitySet = new AvailabilitySetData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG", "rg"},
                        {"testTag", "1"},
                    },
            };

            AvailabilitySet availabilitySet = await _availabilitySetContainer.CreateOrUpdateAsync(
                inputAvailabilitySetName,
                inputAvailabilitySet);

            // List AvailabilitySets
            string expectedAvailabilitySetId = Helpers.GetAvailabilitySetRef(TestEnvironment.SubscriptionId, resourceGroup1Name, inputAvailabilitySetName);
            var listResponse = _availabilitySetContainer.ListAsync();
            var listResponseList = await listResponse.ToEnumerableAsync();
            ValidateAvailabilitySet(inputAvailabilitySet, listResponseList.Select(set => set.Data).FirstOrDefault(x => x.Name == inputAvailabilitySetName),
                inputAvailabilitySetName, expectedAvailabilitySetId, defaultFD, defaultUD);

            var updateKey = "UpdateTag";
            AvailabilitySet updateTagResponse = await availabilitySet.AddTagAsync(updateKey, "updateValue");

            Assert.True(updateTagResponse.Data.Tags.ContainsKey(updateKey));

            // This call will also delete the Availability Set
            await ValidateResults(availabilitySet.Data, inputAvailabilitySet, resourceGroup1Name, inputAvailabilitySetName, defaultFD, defaultUD);
        }

        private async Task VerifyNonDefaultValuesSucceed()
        {
            // Negative tests for a bug in 5.0.0 that read-only fields have side-effect on the request body
            var testStatus = new InstanceViewStatus
            {
                Code = "test",
                DisplayStatus = "test",
                Message = "test"
            };

            string inputAvailabilitySetName = Recording.GenerateAssetName("asnondefault");
            var inputAvailabilitySet = new AvailabilitySetData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG", "rg"},
                        {"testTag", "1"},
                    },
                PlatformFaultDomainCount = nonDefaultFD,
                PlatformUpdateDomainCount = nonDefaultUD
            };

            AvailabilitySet availabilitySet = await _availabilitySetContainer.CreateOrUpdateAsync(
                inputAvailabilitySetName,
                inputAvailabilitySet);

            // This call will also delete the Availability Set
            await ValidateResults(availabilitySet.Data, inputAvailabilitySet, resourceGroup1Name, inputAvailabilitySetName, nonDefaultFD, nonDefaultUD);
        }

        private async Task ValidateResults(AvailabilitySetData outputAvailabilitySet, AvailabilitySetData inputAvailabilitySet, string resourceGroupName, string inputAvailabilitySetName, int expectedFD, int expectedUD)
        {
            string expectedAvailabilitySetId = Helpers.GetAvailabilitySetRef(TestEnvironment.SubscriptionId, resourceGroupName, inputAvailabilitySetName);

            Assert.True(outputAvailabilitySet.Name == inputAvailabilitySetName);
            Assert.True(outputAvailabilitySet.Location == TestEnvironment.Location
                     || outputAvailabilitySet.Location == inputAvailabilitySet.Location);

            ValidateAvailabilitySet(inputAvailabilitySet, outputAvailabilitySet, inputAvailabilitySetName, expectedAvailabilitySetId, expectedFD, expectedUD);

            // GET AvailabilitySet
            AvailabilitySet availabitySet = await _availabilitySetContainer.GetAsync(inputAvailabilitySetName);
            ValidateAvailabilitySet(inputAvailabilitySet, availabitySet.Data, inputAvailabilitySetName, expectedAvailabilitySetId, expectedFD, expectedUD);

            // List VM Sizes
            // TODO -- this method is not generated
            //var listVMSizesResponse = getResponse.Value.ListAvailableSizesAsync(resourceGroupName, inputAvailabilitySetName);
            //var listVMSizesResp = await listVMSizesResponse.ToEnumerableAsync();
            //Helpers.ValidateVirtualMachineSizeListResponse(listVMSizesResp);

            // Delete AvailabilitySet
            await availabitySet.DeleteAsync();
        }

        private void ValidateAvailabilitySet(AvailabilitySetData inputAvailabilitySet, AvailabilitySetData outputAvailabilitySet, string inputAvailabilitySetName, string expectedAvailabilitySetId, int expectedFD, int expectedUD)
        {
            Assert.True(inputAvailabilitySetName == outputAvailabilitySet.Name);
            Assert.True(outputAvailabilitySet.Type == ApiConstants.ResourceProviderNamespace + "/" + ApiConstants.AvailabilitySets);

            Assert.True(outputAvailabilitySet != null);
            Assert.True(outputAvailabilitySet.PlatformFaultDomainCount == expectedFD);
            Assert.True(outputAvailabilitySet.PlatformUpdateDomainCount == expectedUD);

            Assert.NotNull(inputAvailabilitySet.Tags);
            Assert.NotNull(outputAvailabilitySet.Tags);

            foreach (var tag in inputAvailabilitySet.Tags)
            {
                string key = tag.Key;
                Assert.True(inputAvailabilitySet.Tags[key] == outputAvailabilitySet.Tags[key]);
            }
            // TODO: Dev work corresponding to setting status is not yet checked in.
            //Assert.NotNull(outputAvailabilitySet.Properties.Id);
            //Assert.True(expectedAvailabilitySetIds.ToLowerInvariant() == outputAvailabilitySet.Properties.Id.ToLowerInvariant());
        }

        // Make sure availability sets across resource groups are listed successfully
        private async Task VerifyListAvailabilitySetsInSubscription()
        {
            string resourceGroup2Name = baseResourceGroupName + "_2";
            string baseInputAvailabilitySetName = Recording.GenerateAssetName("asdefaultvalues");
            string availabilitySet1Name = baseInputAvailabilitySetName + "_1";
            string availabilitySet2Name = baseInputAvailabilitySetName + "_2";

            //try
            //{
            var inputAvailabilitySet1 = new AvailabilitySetData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG1", "rg1"},
                        {"testTag", "1"},
                    },
            };
            AvailabilitySet outputAvailabilitySet1 = await _availabilitySetContainer.CreateOrUpdateAsync(availabilitySet1Name, inputAvailabilitySet1);

            _resourceGroup2 = await ResourceGroupContainer.CreateOrUpdateAsync(
                resourceGroup2Name,
                new ResourceGroupData(TestEnvironment.Location)
                {
                    Tags = { { resourceGroup2Name, Recording.UtcNow.ToString("u") } }
                });

            var inputAvailabilitySet2 = new AvailabilitySetData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG2", "rg2"},
                        {"testTag", "2"},
                    },
            };
            AvailabilitySet outputAvailabilitySet2 = await _availabilitySetContainer.CreateOrUpdateAsync(availabilitySet2Name, inputAvailabilitySet2);
            var response = DefaultSubscription.ListAvailabilitySetsAsync();

            await foreach (var availabilitySet in response)
            {
                if (availabilitySet.Data.Name == availabilitySet1Name)
                {
                    Assert.AreEqual(inputAvailabilitySet1.Location, availabilitySet.Data.Location);
                    Assert.IsEmpty(availabilitySet.Data.VirtualMachines);
                }
                else if (availabilitySet.Data.Name == availabilitySet2Name)
                {
                    Assert.AreEqual(inputAvailabilitySet2.Location, availabilitySet.Data.Location);
                    Assert.IsEmpty(availabilitySet.Data.VirtualMachines);
                }
            }

            response = DefaultSubscription.ListAvailabilitySetsAsync("virtualMachines/$ref");
            int validationCount = 0;

            await foreach (var availabilitySet in response)
            {
                Assert.NotNull(availabilitySet.Data.VirtualMachines);
                if (availabilitySet.Data.Name == availabilitySet1Name)
                {
                    Assert.AreEqual(0, availabilitySet.Data.VirtualMachines.Count);
                    await ValidateResults(outputAvailabilitySet1.Data, inputAvailabilitySet1, resourceGroup1Name, availabilitySet1Name, defaultFD, defaultUD);
                    validationCount++;
                }
                else if (availabilitySet.Data.Name == availabilitySet2Name)
                {
                    Assert.AreEqual(0, availabilitySet.Data.VirtualMachines.Count);
                    await ValidateResults(outputAvailabilitySet2.Data, inputAvailabilitySet2, resourceGroup2Name, availabilitySet2Name, defaultFD, defaultUD);
                    validationCount++;
                }
            }

            Assert.True(validationCount == 2);
        }

        private async Task Initialize()
        {
            baseResourceGroupName = Recording.GenerateAssetName(TestPrefix);
            resourceGroup1Name = baseResourceGroupName + "_AS";

            _resourceGroup1 = await ResourceGroupContainer.CreateOrUpdateAsync(
                resourceGroup1Name,
                new ResourceGroupData(TestEnvironment.Location)
                {
                    Tags = { { resourceGroup1Name, Recording.UtcNow.ToString("u") } }
                });
            _availabilitySetContainer = _resourceGroup1.GetAvailabilitySets();
        }
    }
}
