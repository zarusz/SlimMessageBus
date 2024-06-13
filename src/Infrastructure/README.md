# Local infrastructure
 
Local containers to facilitate testing for all messages buses (except for Azure Event Hub and Azure Service Bus) can be created and hosted using [Docker Desktop](https://www.docker.com/products/docker-desktop/). 

No volumes have been configured so as to provide clean instances on each run.
 
 ```
src/Infrastructure> docker compose up --force-recreate -V
 ```
 or
 ```
 > ./infrstructure.sh
 ```

## Configuration

Personal instances of Azure resources are required as containers are not available for those services.

1. Copy `src/secrets.txt.sample` to `src/secrets.txt`
2. Supply personal instances of Azure Event Hub and Azure Service Bus
3. Add connection strings to `src/secrets.txt` for 
	- Azure Service Bus
	- Azure Event Hub


### Azure Event Hub
The transport/plug-in does not provide topology provisioning. The below event hubs and consumer groups will need to be manually added to the Azure Event Hub instance for the integration tests to run.

| Event Hub			| Consumer Group	|
|---------------------------------------|
| test-ping			| subscriber		|
| test-echo			| handler			|
| test-echo-resp	| response-reader	|