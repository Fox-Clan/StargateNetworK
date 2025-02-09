using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.Loader;
using WebSocketSharp;
using WebSocketSharp.Server;
using SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;




namespace StargateNetwork
{
    class Program
    {
        public class Echo : WebSocketBehavior
        {
            protected override void OnMessage(MessageEventArgs wibi)
            {
                Console.WriteLine("Received message from client :" + wibi.Data);
                
                //Deserialize the incoming message
                string type = "null";
                dynamic message = JsonConvert.DeserializeObject(wibi.Data);
                type = message.type;

                if (type != "null")
                {
                    Console.WriteLine("Received: " + type + " from client");
                    Console.WriteLine("Client id = " + ID);
                    
                    //message handler
                    switch (type)
                    {
                        //used when gate requests initial address during setup
                        case "requestAddress":
                        {
                            string requestedAddress = message.gate_address; //i need to do this because cs is being funny
                            Console.WriteLine("New address request: '" + requestedAddress + "'");
                            
                            //check db if any gates already have the address
                            var db = new SQLiteAsyncConnection("stargates.db");

                            string query =  "SELECT * FROM Stargate WHERE gate_address='" + requestedAddress + "'";
                            var results = db.QueryAsync<Stargate>(query);
                            results.Wait();
                            
                            
                            if (results.Result.Any())
                            {
                                Console.WriteLine("Address in use!");
                                Send("403");
                                break;
                            }
                            
                            //create database entry for stargate
                            Stargate new_stargate = new Stargate()
                            {
                                id = ID,
                                gate_address = message.gate_address,
                                gate_code = message.gate_code,
                                is_headless = message.is_headless,
                                session_url = message.session_id,
                                active_users = message.current_users,
                                max_users = message.max_users,
                                gate_status = "IDLE",
                                session_name = message.gate_name,
                                owner_name = message.host_id,
                                iris_state = false,
                                creation_date = UnixTimestamp(),
                            };
                                
                            db.InsertAsync(new_stargate).ContinueWith((t) =>
                            {
                                Console.WriteLine("Stargate added to database");
                            });
                            
                            break;
                        }
                        
                        //used to make sure the dialed address is valid
                        case "validateAddress":
                        {
                            //TODO
                            break;
                        }
                        
                        //used to make a request to the server to dial a remote gate
                        case "dialRequest":
                        {
                            //TODO add of this is test code
                            //"CSDialCheck:200" opens gate
                            Send("CSDialCheck:200");
                            Send("CSDialedSessionURL:ressession:///S-2b8d4254-08bf-41af-ac4b-4a80464004b4");
                            Console.WriteLine("opening gate");
                            break;
                        }
                        
                        //used to close wormhole on both gates
                        case "closeWormhole":
                        {
                            //TODO
                            break;
                        }
                        
                        //used to update info about the gate on the database
                        case "updateData":
                        {
                            Console.WriteLine("Updated requested");
                            
                            //find gate and update record
                            var db = new SQLiteAsyncConnection("stargates.db");
                            string query = "UPDATE Stargate SET " +
                                "active_users='" + message.currentUsers +"', " +
                                "max_users='" + message.MaxUsers + "', " +
                                "gate_status='" + message.gate_status + "' " +
                                "WHERE gate_address='" + message.gate_address + "'";
                            var results = db.QueryAsync<Stargate>(query);
                            results.Wait();
                            Console.WriteLine("Updated record");
                            
                            break;
                        }
                        
                        //used to update iris state info on the server
                        case "updateIris":
                        {
                            //TODO
                            break;
                        }
                        
                        //keepalive
                        case "keepAlive":
                        {
                            //TODO
                            break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Received invalid message type from client");
                }
            }
        }
        

        public static int UnixTimestamp()
        {
            return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }
        
        static void Main(string[] args)
        {
            //create stargate table
            var db = new SQLiteAsyncConnection("stargates.db");
            db.CreateTableAsync<Stargate>().Wait();
            
            //start websocket server
            WebSocketServer wssv = new WebSocketServer("ws://192.168.1.14:27015");
            wssv.AddWebSocketService<Echo>("/Echo");
            wssv.Start();
            Console.WriteLine("server started");
        
            Console.ReadKey();
            wssv.Stop();
        }
    }
}
