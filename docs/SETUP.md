## Blazor WASM Project Creation

The LabResultsGateway project was created using the following command:

```bash
dotnet new blazorwasm -n LabResultsGateway.Client -au IndividualB2C -o src/Client -p -e -f net9.0
```

The Azure Functions API was created using the command:

```bash
func init src/API --worker-runtime dotnet-isolated --target-framework net9.0 --name LabResultsGateway.API
```
