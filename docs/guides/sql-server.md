# Test Database Setup
1. Open a terminal/PowerShell
1. Create a new folder, name it whatever you want, I used: `mssql`
1. Navigate to the new folder: `cd mssql`
1. Create a new file in that folder. Linux: `touch docker-compose.yml`, powershell: `New-Item -Path "docker-compose.yml" -ItemType File`
1. Open the file for editing: Vim: `nvim docker-compose.yml` or VSCode: `code docker-compose.yml`
1. In the new file paste the following:
   ```yaml
   services:
     mssql:
       image: mcr.microsoft.com/mssql/server:2022-latest
       environment:
         - ACCEPT_EULA=Y
         - MSSQL_SA_PASSWORD=${DB_PASSWORD}
         - MSSQL_PID=Developer
         - TZ=America/Edmonton
         - MSSQL_BACKUP_DIR=/var/opt/mssql/backups
         - MSSQL_LCID=1033
       ports:
         - "1433:1433"
       volumes:
         - ./data/backups:/var/opt/mssql/backups
         - ./data/data:/var/opt/mssql/data
         - ./data/log:/var/opt/mssql/log
   ```
1. Change as needed, I'll list below what each value means, so you know how to change it
1. Save the File
1. Create a new file called: `.env`. Linux: `touch .env`, Powershell: `New-Item -Path ".env" -ItemType File`
1. Open the file for editing. Vim: `nvim .env` or VSCode: `code .env`
1. In the new file paste the following password for the SA account, it can be anything you want but muse be at least 8 characters:
   ```bash
   DB_PASSWORD=Y0u4P@S5w0rd
   ```
1. Start the docker service by running: `docker compose up` you can see the log, when all looks good hit `d` to detach from logs.
1. Terminal/PowerShell can be closed now
1. __Create a database called: RailcarTrips__
1. Create a user secret for the connection string to the new database. Either by `dotnet user-secrets set ConnectionStrings:DefaultConnection="..."` or just updating appsettings.json, the later being far less secure.

## Explanation of docker-compose.yml
### Environment Variables
- __ACCEPT_EULA__ Self explanatory, allows automated install
- __MSSQL_SA_PASSWORD__ This will be the SA User Password, we set it to an environment variable that we can set in the `.env` file
- __MSSQL_PID__ Leave as Developer to avoid licence issues for testing purposes
- __TZ__ This is your timezone. It must match the Locale structure from default Linux installs
- __MSSQL_BACKUP_DIR__ I set this for testing so that the backups aren't lost when I restart the docker container. This can be omitted
- __MSSQL_LCID__ This is the language code for SQL, I set to 1033 for English
### Port
If you already have SQL running on your local machine you may need to change the port redirect from Docker, say you wanted to use "41433" as your local port for connecting. You'd change this to read: `- "41433:1433"`
### Volumes
I made these all relative to the directory you created the `docker-compose.yml` file in. This keeps everything together and easy to delete when testing is complete. Feel free to change to whatever works