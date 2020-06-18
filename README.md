# Project Overview
.NET Framework Console Client which allows events to be received from a Paxton Net2 system (6.03+) and then processed in C# console app.

# Potential Uses
- Send read events to an external database
- Send an email or SMS when a certain card/token is seen
- Post to an API when a tamper alert is raised on a door

# Configuration
Once you've got API access enabled (email Paxton support for help), make a note of the user you will use for API requests. Add your configuration
into the app.config file replacing the holding values with yours. Make sure you also change the ***paxtonBaseUrl*** key to be the address of your Net2 
server. In my case, I'm using the FQDN and default port (http://paxton.company.com:8080). You can check this is correct by browsing the URL you enter here,
which should take you to the homepage of the API.