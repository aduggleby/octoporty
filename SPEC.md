I want to create a reverse proxy solution like ngrok but in .NET.

There shall be three parts to this project:
1. Caddy as the main reverse proxy solution that will terminate SSL.
2. A .NET Service running in Docker that will act as the endpoint for Caddy and wait for connections from inside the network from 3.
3. A .NET Service running Docker that will handle reaching out to 2, establish the connection and forward requests that go to 2 to our internally mapped services.

The basic idea being in 3 I map an internal port to an external domain name and port, by default this will be HTTPS coming in to the domain mapped to a specific HTTP or HTTPS port on the inside. The HTTPS port on the inside may not be trusted/self-signed.

2 and 3 must establish a trust connection somehow, come up with a secure approach. There will only be a single 3 connecting to a single 2. 

3 also includes a Web UI for configuring everything. This will reach out to 2 to configure it which in turn will also reach out to 1 (Caddy) to configure the appropriate mapping there. 

1 and 2 will run on a Hetzner box in the Cloud.
3 will run inside my network not reachable from the outside.


The final artefacts I want is:
- A docker image published to GitHub for 2
- A docker image published to Github for 3
- A setup script that I can run on a fresh Ubuntu 24 Cloud VM in Hetzner that sets up Caddy and the docker image from 2.

The docker image for 3 should use environment variables for the connection string and any other configuraiton settings like the url to the server api for 2.

The Web UI in 3 should be API based so we can later add a CLI and the frontend is React + Tailwind 4 + Vite running inside an ASP.NET server.

2 should be stateless in a sense that if 2 restarts or comes back up it will wait for 3 to reach out (which it does periodically if it cannot connect) and provide it with the current configuration. 2 will then configure itself and check if the Caddy configuration needs to be adapted. The master configuration is always held in 3 using a SQL Server configured there. The SQL Server already exist, so the docker container will simply be provided a connection string.

The technologies I want you to use unless other wise instructed are .NET 10, Entity Framework and FastEndpoints, for the rest come up with good suggestions.

Come up with reasonable names for each of the project parts instead of the numbers but the main project is called octoporty and will later run be hosted at octoporty.com (but my hetzner vm will run under different domains obviously)