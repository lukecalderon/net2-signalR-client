using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using net2_event_push.Classes;

namespace net2_event_push
{
    class Program
    {
        static void Main(string[] args)
        {
            //Override connection limit - default is 2
            ServicePointManager.DefaultConnectionLimit = 10;

            //Call Get API Token task
            Task<string> apiTokenTask = getApiToken();
            string apiTokenTaskResult = apiTokenTask.Result;

            //Decode the result and store token in apiToken string variable
            dynamic resultApiTokenJson = JsonConvert.DeserializeObject<dynamic>(apiTokenTaskResult);
            string apiToken = resultApiTokenJson.access_token;


            try
            {
                //Get the base URL of the API
                var connUrl = ConfigurationManager.AppSettings["paxtonBaseUrl"];
                var apiKey = apiToken;

                //Create connection object, and add API key as a query string
                string tokenAuth = "token=" + apiKey;
                var hubConn = new HubConnection(connUrl, tokenAuth);

                //Logging
                ///Change TraveLevels as appropriate for debugging
                hubConn.TraceLevel = TraceLevels.StateChanges;
                hubConn.TraceWriter = Console.Out;

                //Create Hub proxy using hub name from the app.config
                string net2EventHub = ConfigurationManager.AppSettings["eventHubName"];
                IHubProxy net2HubProxy = hubConn.CreateHubProxy(net2EventHub);

                //Start Hub Connection
                Console.WriteLine("Connecting to hub...");
                hubConn.Start().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Error opening the connection:{0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Connected!");
                    }
                }).Wait();


                ///////////////////////////////////////////////////////////////////////////////////////////////
                /// LIVE EVENTS
                ///////////////////////////////////////////////////////////////////////////////////////////////

                //Example - Subscribe to Live Events
                Console.WriteLine("Subscribing to live events....");
                net2HubProxy.Invoke("subscribeToLiveEvents").ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed!");

                    }
                }).Wait();

                //Example = Handle live events
                net2HubProxy.On("liveEvents", t =>
                {
                    //Output some info here
                    Console.WriteLine("--- Live Event Received ---");
                    Console.WriteLine("Timestamp: {0}", t[0].eventTime);
                    Console.WriteLine("Door Name: {0}", t[0].areaName);
                    Console.WriteLine("Token Number: {0}", t[0].tokenNumber);
                    Console.WriteLine("--- End of Event ---");

                    //Query API for the user details from the User ID
                    string userId = t[0].userId;

                    //Check if API token is valid or a new one is required
                    if (resultApiTokenJson.expiry_datetime < DateTime.UtcNow) 
                        {
                            Console.WriteLine("Getting new API Token...");
                       
                            //Call Get API Token task
                            Task<string> queryApiTokenTask = getApiToken();
                            string queryApiTokenTaskResult = apiTokenTask.Result;

                            //Decode the result and store token in apiToken string variable
                            dynamic queryResultApiTokenJson = JsonConvert.DeserializeObject<dynamic>(apiTokenTaskResult);
                            apiKey = resultApiTokenJson.access_token;

                        } else
                        {
                            Console.WriteLine("API token is still valid, posting visit...");
                        };

                        //Call Paxton Get User API Endpoint
                        Task<string> userCustomAttributeTask = getPaxtonUser(userId, apiKey);
                        string userCustomAttributeTaskResult = userCustomAttributeTask.Result;

                        //Decode the result and store token in apiToken string variable
                        dynamic resultUserCustomAttributeJson = JsonConvert.DeserializeObject<dynamic>(userCustomAttributeTaskResult);
                        string userCustomAttribute = resultUserCustomAttributeJson.customFields[0].value;

                        //Detect site from door name
                        string doorName = t[0].areaName;
                        string siteName = "";

                        //Detect which site the read was at
                        if (doorName.StartsWith("SiteA"))
                        {   //Example door name -> SiteA Staff Entrance
                            siteName = "SiteA";
                        } else if (doorName.StartsWith("SiteB"))
                        {   //Example door name -> SiteB Finance Office
                            siteName = "SiteB";
                        } else 
                        {   //Something to handle a door not matching the above rules
                            siteName = "Paxton Misconfiguration";
                        };

                        //Put the visit details into a class
                        var visitRecord = new LiveEvent
                        {
                            EventDateTime = t[0].eventTime,
                            Site = siteName,
                            EventLocation = t[0].areaName,
                            RfidTag = t[0].tokenNumber,
                            CustomAttribute1 = userCustomAttribute,
                            EventType = "Paxton"
                        };

                        //Serialise the object ready to POST somewhere
                        var visitJson = JsonConvert.SerializeObject(visitRecord);
                        var visitContent = new StringContent(visitJson, Encoding.UTF8, "application/json");

                    
                });

                ///////////////////////////////////////////////////////////////////////////////////////////////


                ///////////////////////////////////////////////////////////////////////////////////////////////
                /// Door Events
                ///////////////////////////////////////////////////////////////////////////////////////////////

                //Subscribe to door events
                string doorSerial = "062381209";

                Console.WriteLine("Subscribing to door events for serial " + doorSerial + "....");
                net2HubProxy.Invoke("subscribeToDoorEvents", doorSerial).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed!");

                    }
                }).Wait();

                //Handle Events
                net2HubProxy.On("doorEvents", d =>
                {
                    //Output some info here
                    Console.WriteLine("--- Door Event Received ---");
                    Console.WriteLine("Door ID: {0}", d[0].doorId);
                    Console.WriteLine("Door Locked: {0}", d[0].locked);
                    Console.WriteLine("--- End of Event ---");
                });

                ///////////////////////////////////////////////////////////////////////////////////////////////


                ///////////////////////////////////////////////////////////////////////////////////////////////
                /// Door Status Events
                ///////////////////////////////////////////////////////////////////////////////////////////////

                string doorStatusSerial = "012345678";
                Console.WriteLine("Subscribing to door events for serial " + doorStatusSerial + "....");
                net2HubProxy.Invoke("subscribeToDoorStatusEvents", doorStatusSerial).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed!");

                    }
                }).Wait();

                //Handle door status events
                net2HubProxy.On("doorStatusEvents", s =>
                {
                    //Output some info here
                    Console.WriteLine("--- Door Event Received ---");
                    Console.WriteLine("Door ID: {0}", s[0].doorId);
                    Console.WriteLine("Tamper Contact: {0}", s[0]["status"].tamperContactClosed);
                    Console.WriteLine("Alarm Tripped: {0}", s[0]["status"].alarmTripped);
                    Console.WriteLine("--- End of Event ---");
                });

                ///////////////////////////////////////////////////////////////////////////////////////////////


                ///////////////////////////////////////////////////////////////////////////////////////////////
                /// Roll Call Events
                ///////////////////////////////////////////////////////////////////////////////////////////////
                string rollCallId = "1";
                Console.WriteLine("Subscribing to roll call events for ID " + rollCallId + "....");
                net2HubProxy.Invoke("subscribeToRollCallEvents", rollCallId).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Console.WriteLine("Issue calling send: {0}", task.Exception.GetBaseException());
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Subscribed!");

                    }
                }).Wait();

                //Handle roll call events
                net2HubProxy.On("rollCallEvents", s =>
                {
                    //Output some info here
                    Console.WriteLine("--- Door Event Received ---");
                    Console.WriteLine("Roll Call Report ID: {0}", s[0].rollCallReportId);
                    Console.WriteLine("Roll Call Event Type: {0}", s[0].rollCallEventType);
                    Console.WriteLine("--- End of Event ---");
                });

                ///////////////////////////////////////////////////////////////////////////////////////////////


                //Keep console up and running
                Console.ReadLine();

                //Stop connection one exit
                hubConn.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }


        }

        public static async Task<string> getApiToken()
        {
            //Get API token x-www-formurl-encoded content from app.config
            var apiValues = new Dictionary<string, string>
            {
                {"username", ConfigurationManager.AppSettings["net2User"] },
                {"password", ConfigurationManager.AppSettings["net2Password"] },
                {"grant_type", ConfigurationManager.AppSettings["net2GrantType"] },
                {"client_id", ConfigurationManager.AppSettings["net2ClientId"] }
            };

            //Get base URL from app.config, and append the auth token endpoint to it
            var apiUrl = ConfigurationManager.AppSettings["paxtonBaseUrl"] + "/api/v1/authorization/tokens";

            //Encode the request body above, and then POST to the token endpoint
            var apiRequestContent = new FormUrlEncodedContent(apiValues);
            var apiResponse = await client.PostAsync(apiUrl, apiRequestContent);
            string apiResponseString = await apiResponse.Content.ReadAsStringAsync();

            //Return the whole response
            return apiResponseString;
            
        }


        public static async Task<string> getPaxtonUser(string userId, string apiToken)
        {
            //Pick up the base URL from app.config, and append the user endpoint & userID to it
            var userQueryUrl = ConfigurationManager.AppSettings["paxtonBaseUrl"] + "/api/v1/users/" + userId;
            var userQueryRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(userQueryUrl),
                Headers =
                {
                    { HttpRequestHeader.Authorization.ToString(), "Bearer " + apiToken },
                    { HttpRequestHeader.Accept.ToString(), "application/json" }
                }
            };

            //Get the result and output the status code to the console
            HttpResponseMessage userQueryResponse = client.SendAsync(userQueryRequest).Result;

            //var apiResponse = await client.PostAsync(userQueryUrl, apiRequestContent);
            string userQueryResponseString = await userQueryResponse.Content.ReadAsStringAsync();

            //Return the whole response
            return userQueryResponseString;

        }


        //HTTP Client for web requests
        private static readonly HttpClient client = new HttpClient();
    }
}
