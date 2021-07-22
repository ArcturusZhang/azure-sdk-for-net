// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Core.TestFramework;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Core;
using NUnit.Framework;

namespace Azure.ResourceManager.Compute.Tests
{
    public class ProximityPlacementGroupTests : ComputeTestBase
    {
        public ProximityPlacementGroupTests(bool isAsync) : base(isAsync)
        {
            Environment.SetEnvironmentVariable("LOCATION", Location.EastUS2);
        }

        private ResourceGroup resourceGroup1;
        private ResourceGroup resourceGroup2;
        private string baseResourceGroupName;
        private string resourceGroup1Name;
        private ProximityPlacementGroupContainer _proximityPlacementGroupContainer;

        [RecordedTest]
        public async Task TestProximityPlacementGroupsOperations()
        {
            await Initialize();

            //Verify proximityPlacementGroups operation
            VerifyPutPatchGetAndDeleteWithDefaultValues_Success();

            await VerifyPutPatchGetAndDeleteWithInvalidValues_Failure();

            //// Make sure proximityPlacementGroup across resource groups are listed successfully and
            //// proximityPlacementGroups in a resource groups are listed successfully
            //await VerifyListProximityPlacementGroups();
        }

        private async Task Initialize()
        {
            baseResourceGroupName = Recording.GenerateAssetName(TestPrefix);
            resourceGroup1Name = baseResourceGroupName + "_1";

            resourceGroup1 = await ResourceGroupContainer.CreateOrUpdateAsync(
                resourceGroup1Name,
                new ResourceGroupData(TestEnvironment.Location)
                {
                    Tags = { { resourceGroup1Name, Recording.UtcNow.ToString("u") } }
                });
            _proximityPlacementGroupContainer = resourceGroup1.GetProximityPlacementGroups();
        }

        private void VerifyPutPatchGetAndDeleteWithDefaultValues_Success()
        {
            var tags = new Dictionary<string, string>()
            {
                { "RG", "rg"},
                { "testTag", "1"}
            };

            var inputProximityPlacementGroup = new ProximityPlacementGroupData(TestEnvironment.Location);
            inputProximityPlacementGroup.Tags.InitializeFrom(tags);

            var expectedProximityPlacementGroup = new ProximityPlacementGroupData(TestEnvironment.Location)
            {
                ProximityPlacementGroupType = ProximityPlacementGroupType.Standard
            };
            expectedProximityPlacementGroup.Tags.InitializeFrom(tags);

            VerifyPutPatchGetAndDeleteOperations_Scenarios(inputProximityPlacementGroup, expectedProximityPlacementGroup);
        }

        private async void VerifyPutPatchGetAndDeleteOperations_Scenarios(ProximityPlacementGroupData inputProximityPlacementGroup,
            ProximityPlacementGroupData expectedProximityPlacementGroup)
        {
            var proximityPlacementGroupName = Recording.GenerateAssetName("testppg");

            // Create and expect success.
            ProximityPlacementGroup outProximityPlacementGroup = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroupName, inputProximityPlacementGroup);

            ValidateProximityPlacementGroup(expectedProximityPlacementGroup, outProximityPlacementGroup.Data, proximityPlacementGroupName);

            // Get and expect success.
            outProximityPlacementGroup = await _proximityPlacementGroupContainer.GetAsync(proximityPlacementGroupName);
            ValidateProximityPlacementGroup(expectedProximityPlacementGroup, outProximityPlacementGroup.Data, proximityPlacementGroupName);

            // Put and expect failure
            ProximityPlacementGroup failedProximityPlacementGroup;
            try
            {
                //Updating ProximityPlacementGroupType in inputProximityPlacementGroup for a Update call.
                if (expectedProximityPlacementGroup.ProximityPlacementGroupType == ProximityPlacementGroupType.Standard)
                {
                    inputProximityPlacementGroup.ProximityPlacementGroupType = ProximityPlacementGroupType.Ultra;
                }
                else
                {
                    inputProximityPlacementGroup.ProximityPlacementGroupType = ProximityPlacementGroupType.Standard;
                }

                failedProximityPlacementGroup = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroupName, inputProximityPlacementGroup);
            }
            catch (Exception ex)
            {
                //if (ex.StatusCode == HttpStatusCode.Conflict)
                //{
                //    Assert.AreEqual("Changing property 'proximityPlacementGroup.properties.proximityPlacementGroupType' is not allowed.", ex.Message );
                //}
                //else if (ex.Response.StatusCode == HttpStatusCode.BadRequest)
                //{
                //    Assert.Equal("The subscription is not registered for private preview of Ultra Proximity Placement Groups.", ex.Message, StringComparer.OrdinalIgnoreCase);
                //}
                //else
                //{
                //    Console.WriteLine($"Expecting HttpStatusCode { HttpStatusCode.Conflict} or { HttpStatusCode.BadRequest}, while actual HttpStatusCode is { ex.Response.StatusCode}.");
                //    throw;
                //}
                Console.WriteLine($"Expecting HttpStatusCode { HttpStatusCode.Conflict} or { HttpStatusCode.BadRequest}, while actual HttpStatusCode is { ex.Message}.");
                throw;
            }
            Assert.True(failedProximityPlacementGroup == null, "ProximityPlacementGroup in response should be null.");

            // Clean up
            await outProximityPlacementGroup.DeleteAsync();
        }

        private async Task VerifyPutPatchGetAndDeleteWithInvalidValues_Failure()
        {
            var proximityPlacementGroupName = Recording.GenerateAssetName("testppg");
            var inputProximityPlacementGroup = new ProximityPlacementGroupData("")
            {
                Tags =
                {
                    {"RG", "rg"},
                    {"testTag", "1"},
                },
            };
            // Put and expect failure
            ProximityPlacementGroup expectedProximityPlacementGroup = null;

            async void CreateAndExpectFailure()
            {
                try
                {
                    expectedProximityPlacementGroup = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroupName, inputProximityPlacementGroup);
                }
                catch (Exception ex)
                {
                    Assert.NotNull(ex);
                    //Assert.True(ex.Response.StatusCode == HttpStatusCode.BadRequest, $"Expecting HttpStatusCode {HttpStatusCode.BadRequest}, while actual HttpStatusCode is {ex.Response.StatusCode}.");
                }
            }

            //Verify failure when location is invalid
            CreateAndExpectFailure();

            //Verify failure when ProximityPlacementGroupType is invalid
            inputProximityPlacementGroup.Location = TestEnvironment.Location;
            inputProximityPlacementGroup.ProximityPlacementGroupType = "Invalid";
            CreateAndExpectFailure();

            //Verify success when ProximityPlacementGroup is valid
            inputProximityPlacementGroup.ProximityPlacementGroupType = ProximityPlacementGroupType.Standard;
            expectedProximityPlacementGroup = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroupName, inputProximityPlacementGroup);

            ValidateProximityPlacementGroup(inputProximityPlacementGroup, expectedProximityPlacementGroup.Data, proximityPlacementGroupName);

            // Get and expect success.
            expectedProximityPlacementGroup = await _proximityPlacementGroupContainer.GetAsync(proximityPlacementGroupName);
            ValidateProximityPlacementGroup(inputProximityPlacementGroup, expectedProximityPlacementGroup.Data, proximityPlacementGroupName);

            // Clean up
            await expectedProximityPlacementGroup.DeleteAsync();
        }

        // Make sure proximityPlacementGroup across resource groups are listed successfully and proximityPlacementGroups in a resource groups are listed successfully
        private async Task VerifyListProximityPlacementGroups()
        {
            string resourceGroup2Name = baseResourceGroupName + "_2";
            string baseInputProximityPlacementGroupName = Recording.GenerateAssetName("testppg");
            string proximityPlacementGroup1Name = baseInputProximityPlacementGroupName + "_1";
            string proximityPlacementGroup2Name = baseInputProximityPlacementGroupName + "_2";
            var inputProximityPlacementGroup1 = new ProximityPlacementGroupData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG1", "rg1"},
                        {"testTag", "1"},
                    },
            };
            ProximityPlacementGroup outputProximityPlacementGroup1 = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroup1Name, inputProximityPlacementGroup1);

            resourceGroup2 = await ResourceGroupContainer.CreateOrUpdateAsync(
                resourceGroup2Name,
                new ResourceGroupData(TestEnvironment.Location)
                {
                    Tags = { { resourceGroup2Name, Recording.UtcNow.ToString("u") } }
                });

            var inputProximityPlacementGroup2 = new ProximityPlacementGroupData(TestEnvironment.Location)
            {
                Tags =
                    {
                        {"RG2", "rg2"},
                        {"testTag", "2"},
                    },
            };
            ProximityPlacementGroup outputProximityPlacementGroup2 = await _proximityPlacementGroupContainer.CreateOrUpdateAsync(proximityPlacementGroup2Name, inputProximityPlacementGroup2);

            //verify proximityPlacementGroup across resource groups are listed successfully
            //IPage<ProximityPlacementGroup> response = await ProximityPlacementGroupsClient.ListBySubscription();
            var response = DefaultSubscription.ListProximityPlacementGroupsAsync();
            //Assert.True(response.NextPageLink == null, "NextPageLink should be null in response.");

            int validationCount = 0;

            await foreach (ProximityPlacementGroup proximityPlacementGroup in response)
            {
                if (proximityPlacementGroup.Data.Name == proximityPlacementGroup1Name)
                {
                    //PPG is created using default value, updating the default value in input for validation of expected returned value.
                    inputProximityPlacementGroup1.ProximityPlacementGroupType = ProximityPlacementGroupType.Standard;
                    ValidateResults(outputProximityPlacementGroup1.Data, inputProximityPlacementGroup1, resourceGroup1Name, proximityPlacementGroup1Name);
                    validationCount++;
                }
                else if (proximityPlacementGroup.Data.Name == proximityPlacementGroup2Name)
                {
                    //PPG is created using default value, updating the default value in input for validation of expected returned value.
                    inputProximityPlacementGroup2.ProximityPlacementGroupType = ProximityPlacementGroupType.Standard;
                    ValidateResults(outputProximityPlacementGroup2.Data, inputProximityPlacementGroup2, resourceGroup2Name, proximityPlacementGroup2Name);
                    validationCount++;
                }
            }

            Assert.True(validationCount == 2, "Not all ProximityPlacementGroups are returned in response.");
        }

        private async void ValidateResults(ProximityPlacementGroupData outputProximityPlacementGroup, ProximityPlacementGroupData inputProximityPlacementGroup,
            string resourceGroupName, string inputProximityPlacementGroupName)
        {
            Assert.True(outputProximityPlacementGroup.Name == inputProximityPlacementGroupName, "ProximityPlacementGroup.Name mismatch between request and response.");
            Assert.True(outputProximityPlacementGroup.Location == TestEnvironment.Location
                     || outputProximityPlacementGroup.Location == inputProximityPlacementGroup.Location,
                     "ProximityPlacementGroup.Location mismatch between request and response.");

            ValidateProximityPlacementGroup(inputProximityPlacementGroup, outputProximityPlacementGroup, inputProximityPlacementGroupName);

            // GET ProximityPlacementGroup
            ProximityPlacementGroup getResponse = await _proximityPlacementGroupContainer.GetAsync(inputProximityPlacementGroupName);
            ValidateProximityPlacementGroup(inputProximityPlacementGroup, getResponse.Data, inputProximityPlacementGroupName);
        }

        private void ValidateProximityPlacementGroup(ProximityPlacementGroupData expectedProximityPlacementGroup, ProximityPlacementGroupData outputProximityPlacementGroup,
            string expectedProximityPlacementGroupName)
        {
            Assert.True(outputProximityPlacementGroup != null, "ProximityPlacementGroup is null in response.");
            Assert.True(expectedProximityPlacementGroupName == outputProximityPlacementGroup.Name, "ProximityPlacementGroup.Name in response mismatch with expected value.");
            Assert.True(
                outputProximityPlacementGroup.Type == ApiConstants.ResourceProviderNamespace + "/" + ApiConstants.ProximityPlacementGroups,
                "ProximityPlacementGroup.Type in response mismatch with expected value.");

            Assert.True(
                expectedProximityPlacementGroup.ProximityPlacementGroupType == outputProximityPlacementGroup.ProximityPlacementGroupType,
                "ProximityPlacementGroup.ProximityPlacementGroupType in response mismatch with expected value.");

            void VerifySubResource(IReadOnlyList<SubResourceWithColocationStatus> inResource,
                IReadOnlyList<SubResourceWithColocationStatus> outResource, string subResourceTypeName)
            {
                if (inResource == null)
                {
                    Assert.True(outResource == null || outResource.Count == 0, $"{subResourceTypeName} reference in response should be null/empty.");
                }
                else
                {
                    List<ResourceIdentifier> inResourceIds = inResource.Select(input => input.Id).ToList();
                    List<ResourceIdentifier> outResourceIds = outResource.Select(output => output.Id).ToList();
                    Assert.True(inResourceIds.Count == outResourceIds.Count, $"Number of {subResourceTypeName} reference in response do not match with expected value.");
                    Assert.True(0 == inResourceIds.Except(outResourceIds).Count(), $"Response has some unexpected {subResourceTypeName}.");
                }
            }

            VerifySubResource(expectedProximityPlacementGroup.AvailabilitySets, outputProximityPlacementGroup.AvailabilitySets, "AvailabilitySet");
            VerifySubResource(expectedProximityPlacementGroup.VirtualMachines, outputProximityPlacementGroup.VirtualMachines, "VirtualMachine");
            VerifySubResource(expectedProximityPlacementGroup.VirtualMachineScaleSets, outputProximityPlacementGroup.VirtualMachineScaleSets, "VirtualMachineScaleSet");

            Assert.True(expectedProximityPlacementGroup.Tags != null, "Expected ProximityPlacementGroup tags should not be null.");
            Assert.True(outputProximityPlacementGroup.Tags != null, "ProximityPlacementGroup tags in response should not be null.");
            Assert.True(expectedProximityPlacementGroup.Tags.Count == outputProximityPlacementGroup.Tags.Count, "Number of tags in response do not match with expected value.");

            foreach (var tag in expectedProximityPlacementGroup.Tags)
            {
                string key = tag.Key;
                Assert.True(expectedProximityPlacementGroup.Tags[key] == outputProximityPlacementGroup.Tags[key], "Unexpected ProximityPlacementGroup tag is found in response.");
            }
        }

        public void ValidateColocationStatus(InstanceViewStatus expectedColocationStatus, InstanceViewStatus actualColocationStatus)
        {
            Assert.True(expectedColocationStatus.Code == actualColocationStatus.Code, "ColocationStatus code do not match with expected value.");
            Assert.True(expectedColocationStatus.Level == actualColocationStatus.Level, "ColocationStatus level do not match with expected value.");
            Assert.True(expectedColocationStatus.DisplayStatus == actualColocationStatus.DisplayStatus, "ColocationStatus display status do not match with expected value.");
            Assert.True(expectedColocationStatus.Message == actualColocationStatus.Message, "ColocationStatus message do not match with expected value.");
        }
    }
}
