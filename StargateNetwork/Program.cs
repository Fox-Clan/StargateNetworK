using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.Loader;
using WebSocketSharp;
using WebSocketSharp.Server;
using SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Threading.Tasks;





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
                            Console.WriteLine("Dial Requested");
                            //query database for requested gate
                            string query =  "SELECT * FROM Stargate WHERE gate_address='" + message.gate_address + "'";
                            var db = new SQLiteAsyncConnection("stargates.db");
                            var results = db.QueryAsync<Stargate>(query);
                            results.Wait();
                            
                            //query database for current gate and check if it exists
                            var queryLocal =  "SELECT * FROM Stargate WHERE id='" + ID + "'";
                            var resultsLocal = db.QueryAsync<Stargate>(queryLocal);
                            resultsLocal.Wait();
                            if (!resultsLocal.Result.Any())
                            {
                                Console.WriteLine("Local gate not found. Is the gate registered?");
                                Send("CSDialCheck:404");
                                break;
                            }
                            Stargate currentGate = resultsLocal.Result[0];
                            
                            //check if requested gate exists
                            if (!results.Result.Any())
                            {
                                Console.WriteLine("No stargate found");
                                Send("CSDialCheck:404");
                                break;
                            }
                            Stargate requestedGate = results.Result[0];
                            
                            //check if gate is trying to dial itself
                            if (requestedGate.gate_address == currentGate.gate_address)
                            {
                                Console.WriteLine("Gate is trying to dial itself!!!");
                                Send("CSDialCheck:403");
                                break;
                            }
                            
                            //find chev count to send to requested gate
                            string gate_address = message.gate_address;
                            string currentGateCode = currentGate.gate_code;
                            int chevCount = 0;
                            
                            switch(gate_address.Length)
                            {
                                case 6:
                                {
                                    if (requestedGate.gate_code == currentGateCode)
                                    {
                                        chevCount = 6;
                                    }
                                    else
                                    {
                                        chevCount = -1;
                                    }
                                    
                                    break;
                                }

                                case 7:
                                {
                                    if (gate_address.Substring(7,7) == currentGateCode.Substring(7,7))
                                    {
                                        chevCount = 7;
                                    }
                                    else
                                    {
                                        chevCount = -1;
                                    }
                                    
                                    break;
                                }

                                case 8:
                                {
                                    if (gate_address.Substring(7,8) == currentGateCode.Substring(7,8))
                                    {
                                        chevCount = 9;
                                    }
                                    else
                                    {
                                        chevCount = -1;
                                    }
                                    
                                    break;
                                }
                            }

                            if (chevCount == -1)
                            {
                                Console.WriteLine("Invalid gate code!");
                                Send("CSDialCheck:302");
                                break;
                            }

                            //check if destination is full
                            if (requestedGate.active_users >= requestedGate.max_users)
                            {
                                Console.WriteLine("Max users reached on requested session!");
                                Send("CSDialCheck:403");
                                break;
                            }
                            
                            //update gate states on database
                            string updateQueryRequested = "UPDATE Stargate SET " +
                                           "gate_status='INCOMING', " +
                                           "WHERE id='" + requestedGate.id + "'";
                            db.QueryAsync<Stargate>(updateQueryRequested);
                            
                            string updateQueryCurrent = "UPDATE Stargate SET " + 
                                                          "gate_status='OPEN', " +
                                                          "dialed_gate_id='" + requestedGate.id + "', " +
                                                          "WHERE id='" + currentGate.id + "' " + "COMMIT";
                            db.QueryAsync<Stargate>(updateQueryCurrent);
                            
                            //dial gate
                            Send("CSDialCheck:200");
                            Send("CSDialedSessionURL:" + requestedGate.session_url);
                            
                            switch (chevCount)
                            {
                                case 6:
                                {
                                    Sessions.SendTo("Impulse:OpenIncoming:7", requestedGate.id);
                                    break;
                                }
                                
                                case 7:
                                {
                                    Sessions.SendTo("Impulse:OpenIncoming:8", requestedGate.id);
                                    break;
                                }
                                
                                case 8:
                                {
                                    Sessions.SendTo("Impulse:OpenIncoming:9", requestedGate.id);
                                    break;
                                }
                            }
                            Console.Write("Stargate open!");
                            
                            break;
                        }
                        
                        //used to close wormhole on both gates
                        case "closeWormhole":
                        {
                            //query database for current gate
                            string query =  "SELECT * FROM Stargate WHERE id='" + ID + "'";
                            var db = new SQLiteAsyncConnection("stargates.db");
                            var results = db.QueryAsync<Stargate>(query);
                            results.Wait();
                            Stargate currentGate = results.Result[0];
                            
                            //close remote gate
                            Console.WriteLine("Closing wormhole: " + currentGate.dialed_gate_id);
                            Sessions.SendTo("Impulse:CloseWormhole", currentGate.dialed_gate_id);
                            
                            //update gate states on database
                            string updateQueryRequested = "UPDATE Stargate SET " +
                                                          "dialed_gate_id='', " +
                                                          "WHERE id='" + currentGate.id + "'";
                            db.QueryAsync<Stargate>(updateQueryRequested);
                            
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
                                "gate_status='" + message.gate_status + "', " +
                                "update_date='" + UnixTimestamp() + "' " +
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
