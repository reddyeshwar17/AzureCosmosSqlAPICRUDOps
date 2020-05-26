﻿using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;

namespace AzureCosmosSqlAPICRUDOps
{
    class Program
    {
        /* private const string EndpointUrl = "https://crudeops.documents.azure.com:443/";
         private const string AuthorizationKey = "IbT2aRiMF09NFTcVVJSXF0jCZze8zhJxWpTqKcQMQdVMqwVHjX6F8JSPGxQrnuDBrVfwHxFHwTdeuv2g33gujA==";
         private const string DatabaseId = "FamilyDatabase";
         private const string ContainerId = "FamilyContainer";*/

        private static string EndpointUrl = ConfigurationManager.AppSettings["EndpointUrl"];
        private static string AuthorizationKey = ConfigurationManager.AppSettings["AuthorizationKey"];
        private static string DatabaseId = ConfigurationManager.AppSettings["DatabaseId"];
        private static string ContainerId = ConfigurationManager.AppSettings["ContainerId"];

        static async Task Main(string[] args)
        {
            CosmosClient cosmosClient = new CosmosClient(EndpointUrl, AuthorizationKey);
            await CreateDatabaseAsync(cosmosClient);
            await CreateContainerAsync(cosmosClient);
            await AddItemsToContainerAsync(cosmosClient);
            await QueryItemsAsync(cosmosClient);
            await ReplaceFamilyItemAsync(cosmosClient);
            //await DeleteFamilyItemAsync(cosmosClient);
            //await DeleteDatabaseAndCleanupAsync(cosmosClient);
        }

        /// <summary>
        /// Delete the database and dispose of the Cosmos Client instance
        /// </summary>
        private static async Task DeleteDatabaseAndCleanupAsync(CosmosClient cosmosClient)
        {
            Database database = cosmosClient.GetDatabase(Program.DatabaseId);
            DatabaseResponse databaseResourceResponse = await database.DeleteAsync();

            Console.WriteLine("Deleted Database: {0}\n", Program.DatabaseId);
        }
        /// <summary>
        /// Delete an item in the container
        /// </summary>
        private static async Task DeleteFamilyItemAsync(CosmosClient cosmosClient)
        {
            Container container = cosmosClient.GetContainer(DatabaseId, ContainerId);

            string partitionKeyValue = "Wakefield";
            string familyId = "Wakefield.7";

            // Delete an item. Note we must provide the partition key value and id of the item to delete
            ItemResponse<Family> wakefieldFamilyResponse = await container.DeleteItemAsync<Family>(familyId, new PartitionKey(partitionKeyValue));
            Console.WriteLine("Deleted Family [{0},{1}]\n", partitionKeyValue, familyId);
        }

        /// <summary>
        /// Run a query (using Azure Cosmos DB SQL syntax) against the container
        /// </summary>
        private static async Task QueryItemsAsync(CosmosClient cosmosClient)
        {
            var sqlQueryText = "SELECT * FROM c WHERE c.LastName = 'Andersen'";

            Console.WriteLine("Running query: {0}\n", sqlQueryText);

            Container container = cosmosClient.GetContainer(Program.DatabaseId, Program.ContainerId);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

            List<Family> families = new List<Family>();
            FeedIterator<Family> feedIteratorFamily = container.GetItemQueryIterator<Family>(queryDefinition);

            while (feedIteratorFamily.HasMoreResults)
            {
                foreach (Family family in await feedIteratorFamily.ReadNextAsync())
                {
                    families.Add(family);
                    Console.WriteLine("\tRead {0}\n", family);
                }
            }
        }

        /// <summary>
        /// Replace an item in the container
        /// </summary>
        private static async Task ReplaceFamilyItemAsync(CosmosClient cosmosClient)
        {
            Container container = cosmosClient.GetContainer(Program.DatabaseId, Program.ContainerId);

            ItemResponse<Family> wakefieldFamilyResponse = await container.ReadItemAsync<Family>("Wakefield.7", new PartitionKey("Wakefield"));
            Family itemBody = wakefieldFamilyResponse;

            // update registration status from false to true
            itemBody.IsRegistered = true;
            // update grade of child
            itemBody.Children[0].Grade = 6;

            // replace the item with the updated content
            wakefieldFamilyResponse = await container.ReplaceItemAsync<Family>(itemBody, itemBody.Id, new PartitionKey(itemBody.LastName));
            Console.WriteLine("Updated Family [{0},{1}].\n \tBody is now: {2}\n", itemBody.LastName, itemBody.Id, wakefieldFamilyResponse.Resource.Id);
        }
        /// <summary>
        /// Create the container if it does not exist. 
        /// Specify "/LastName" as the partition key since we're storing family information, to ensure good distribution of requests and storage.
        /// </summary>
        /// <returns></returns>
        private static async Task CreateContainerAsync(CosmosClient cosmosClient)
        {
            // Create a new container
            var container = await cosmosClient.GetDatabase(Program.DatabaseId).CreateContainerIfNotExistsAsync(Program.ContainerId, "/LastName");
            Console.WriteLine("Created Container: {0}\n", container.Container.Id);
        }

        /// <summary>
        /// Add Family items to the container
        /// </summary>
        private static async Task AddItemsToContainerAsync(CosmosClient cosmosClient)
        {
            // Create a family object for the Andersen family
            Family andersenFamily = new Family
            {
                Id = "Andersen.1",
                LastName = "Andersen",
                Parents = new Parent[]
                {
            new Parent { FirstName = "Thomas" },
            new Parent { FirstName = "Mary Kay" }
                },
                Children = new Child[]
                {
            new Child
            {
                FirstName = "Henriette Thaulow",
                Gender = "female",
                Grade = 5,
                Pets = new Pet[]
                {
                    new Pet { GivenName = "Fluffy" }
                }
            }
                },
                Address = new Address { State = "WA", County = "King", City = "Seattle" },
                IsRegistered = false
            };

            var container = cosmosClient.GetContainer(Program.DatabaseId, Program.ContainerId);
            try
            {
                // Read the item to see if it exists.  
                ItemResponse<Family> andersenFamilyResponse = await container.ReadItemAsync<Family>(andersenFamily.Id, new PartitionKey(andersenFamily.LastName));
                Console.WriteLine("Item in database with id: {0} already exists\n", andersenFamilyResponse.Resource.Id);
            }
            catch (CosmosException ex) /*when (ex.SubStatusCode == (int)HttpStatusCode.NotFound)*/
            {
                // Create an item in the container representing the Andersen family. Note we provide the value of the partition key for this item, which is "Andersen"
                ItemResponse<Family> andersenFamilyResponse = await container.CreateItemAsync<Family>(andersenFamily, new PartitionKey(andersenFamily.LastName));

                // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse.
                Console.WriteLine("Created item in database with id: {0}\n", andersenFamilyResponse.Resource.Id);
            }

            // Create a family object for the Wakefield family
            Family wakefieldFamily = new Family
            {
                Id = "Wakefield.7",
                LastName = "Wakefield",
                Parents = new Parent[]
                {
            new Parent { FamilyName = "Wakefield", FirstName = "Robin" },
            new Parent { FamilyName = "Miller", FirstName = "Ben" }
                },
                Children = new Child[]
                {
            new Child
            {
                FamilyName = "Merriam",
                FirstName = "Jesse",
                Gender = "female",
                Grade = 8,
                Pets = new Pet[]
                {
                    new Pet { GivenName = "Goofy" },
                    new Pet { GivenName = "Shadow" }
                }
            },
            new Child
            {
                FamilyName = "Miller",
                FirstName = "Lisa",
                Gender = "female",
                Grade = 1
            }
                },
                Address = new Address { State = "NY", County = "Manhattan", City = "NY" },
                IsRegistered = true
            };

            // Create an item in the container representing the Wakefield family. Note we provide the value of the partition key for this item, which is "Wakefield"
            ItemResponse<Family> wakefieldFamilyResponse = await container.UpsertItemAsync<Family>(wakefieldFamily, new PartitionKey(wakefieldFamily.LastName));

            // Note that after creating the item, we can access the body of the item with the Resource property off the ItemResponse. We can also access the RequestCharge property to see the amount of RUs consumed on this request.
            // Console.WriteLine("Created item in database with id: {0}\n", wakefieldFamilyResponse.Value.Id);
        }

        private static async Task CreateDatabaseAsync(CosmosClient cosmosClient)
        {
            var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(Program.DatabaseId);
            Console.WriteLine("Created Database: {0}\n", database.Database.Id);
        }
    }
}
