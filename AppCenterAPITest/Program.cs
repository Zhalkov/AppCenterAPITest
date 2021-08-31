using System;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AppCenterAPITest
{
    class Program
    {
        private static string UserAPIToken;
        private static int retryCount=0;
        static void Main(string[] args)
        {
            string responseString, url;
            string AppName="", Owner="";

            //Enter user data
            if (args.Length != 0)
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("There are 3 required parameters: Application Name, Owner and User API Token");
                    Console.Read();
                    System.Environment.Exit(1);
                }
                else
                {
                    AppName = args[0];
                    Owner = args[1];
                    UserAPIToken = args[2];
                    //Option for setting retry count
                    if (args.Length == 4)
                            retryCount = Convert.ToInt32(args[3]);
                }
            }
            else
            { 
                Console.Write("Enter App name: ");
                AppName = Console.ReadLine();
                Console.Write("Enter owner name: ");
                Owner = Console.ReadLine();
                Console.Write("Enter user API token: ");
                UserAPIToken = Console.ReadLine();
                Console.Write("Enter number of retries: ");
                retryCount = Convert.ToInt32(Console.ReadLine());
            }           

            //Get array of branches
            url = $@"https://api.appcenter.ms/v0.1/apps/{Owner}/{AppName}/branches";
            responseString = APIRequest(url, "GET");

            Console.WriteLine($"Existing branches for {AppName}:");
            JArray BranchesArray = (JArray)JsonConvert.DeserializeObject(responseString);
            foreach (JToken branch in BranchesArray)
            {
                //Get branch name from JSON
                string BranchName = (string)branch["branch"]["name"];
                Console.WriteLine(BranchName);
               
                //Starting build for the branch
                url = $@"https://api.appcenter.ms/v0.1/apps/{Owner}/{AppName}/branches/{BranchName}/builds";
                responseString = APIRequest(url, "POST");                
                
                JToken BuildInfo = (JToken)JsonConvert.DeserializeObject(responseString);
                string BuildtartTime = (string)BuildInfo["queueTime"];
                Console.WriteLine($"Started build for {BranchName} at: {BuildtartTime}");
                Console.WriteLine();
            }

            //Printing info about finished builds for each branch
            Console.WriteLine("Finihed builds:");
            foreach (JToken branch in BranchesArray)
            {
                string BranchName = (string)branch["branch"]["name"];

                //Get array of builds for concrete BranchName from JSON
                url = $@"https://api.appcenter.ms/v0.1/apps/{Owner}/{AppName}/branches/{BranchName}/builds";
                responseString = APIRequest(url, "GET");
                JArray BuildArray = (JArray)JsonConvert.DeserializeObject(responseString);
                
                //Take a look on each build
                foreach (JToken build in BuildArray)
                {
                    //Get status and id for concrete build from JSON
                    string BuildStatus = (string)build["status"];                    
                    
                    if (BuildStatus.Equals("completed"))
                    {
                        //Get id and result of build: completed or failed
                        string BuildResult = (string)build["result"];
                        string BuildId = (string)build["id"];

                        //Get duration of build in seconds
                        DateTime StartTime = Convert.ToDateTime((string)build["startTime"]);
                        DateTime FinishTime = Convert.ToDateTime((string)build["finishTime"]);
                        TimeSpan Duration = FinishTime - StartTime;

                        //Get uri for build logs from JSON
                        url = $@"https://api.appcenter.ms/v0.1/apps/{Owner}/{AppName}/builds/{BuildId}/downloads/logs";
                        responseString = APIRequest(url, "GET");
                        JToken BuildLog = (JToken)JsonConvert.DeserializeObject(responseString);
                        string BuildLogUri = (string)BuildLog["uri"];

                        //Print all info about build
                        Console.WriteLine($"{BranchName} build {BuildResult} in {Duration.TotalSeconds} seconds. Link to build logs: {BuildLogUri}");
                        Console.WriteLine();
                    }
                }
                
            }
            Console.WriteLine("Press any key for exit...");
            Console.Read();
        }

        static string APIRequest (string url, string Method)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Headers.Add("X-API-Token", UserAPIToken);
            request.Method = Method;
            string responseString = "";

            //Sometimes request fails with (401)Unathorized on external reason
            if (retryCount == 0)
                retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    //If request succeeded, stop cycle
                    retryCount = 0;
                }
                catch (System.Net.WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    retryCount--;
                    if (retryCount == 0)
                    {
                        Console.WriteLine("Request failed after several retries. Press any key for exit...");
                        Console.Read();
                        System.Environment.Exit(ex.HResult);
                    }
                }                
            }
            return responseString;
        }
    }
}
