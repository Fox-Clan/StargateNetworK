using System.Diagnostics;
using System.Net.WebSockets;
using System.Runtime.Loader;
using WebSocketSharp;
using WebSocketSharp.Server;
using SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;


namespace StargateNetwork
{
    class Program
    {
        public class Echo : WebSocketBehavior
        {
            protected override async void OnMessage(MessageEventArgs wibi)
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
                            using (var db = new StargateContext())
                            {
                                var gate = await StargateTools.FindGateByAddress(requestedAddress, db);
                                
                                if (gate.id != "NULL")
                                {
                                    bool overRide = false;
                                        
                                    if (UnixTimestamp() - gate.update_date > 60)
                                    {
                                        Console.WriteLine("database entry stale, overriding...");
                                        db.Remove(gate);
                                        overRide = true;
                                    } else if (gate.id == ID)
                                    {
                                        Console.WriteLine("Gate already exists in database. Skipping...");
                                        break;
                                    }

                                    if (!overRide)
                                    {
                                        Console.WriteLine("Address in use");
                                        Send("403");
                                        break;
                                    }
                                }
                                
                                db.Add(new Stargate()
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
                                    iris_state = "false",
                                    creation_date = UnixTimestamp(),
                                    update_date = UnixTimestamp(),
                                    dialed_gate_id = "",
                                });
                                await db.SaveChangesAsync();
                                Send("{code: 200, message: \"Address accepted\" }");
                                Console.WriteLine("Stargate added to database");
                            }
                            
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
                            
                             string requestedAddress = message.gate_address;
                            
                            //query database for requested gate
                            using (var db = new StargateContext())
                            {
                                var requestedGate = await StargateTools.FindGateByAddress(requestedAddress, db); 
                                
                                var currentGate = await StargateTools.FindGateById(ID, db);
                                
                                //check if requested gate exists
                                if (requestedGate.id == "NULL")
                                {
                                    Console.WriteLine("No stargate found");
                                    Send("CSDialCheck:404");
                                    break;
                                }
                                
                                //check if gate is trying to dial itself
                                if (requestedGate.gate_address == currentGate.gate_address)
                                {
                                    Console.WriteLine("Gate is trying to dial itself!!!");
                                    Send("CSDialCheck:403");
                                    break;
                                }
                                
                                //check if destination gate is busy
                                if (requestedGate.gate_status != "IDLE")
                                {
                                    Console.WriteLine("Gate is busy");
                                    Send("CSValidCheck:403");
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
                                requestedGate.gate_status = "INCOMING";

                                currentGate.gate_status = "OPEN";
                                currentGate.dialed_gate_id = requestedGate.id;
                                
                                await db.SaveChangesAsync();
                                
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
                            }
                            
                            break;
                        }
                        
                        //used to close wormhole on both gates
                        case "closeWormhole":
                        {
                            //query database for current gate
                            using (var db = new StargateContext())
                            {
                                var currentGate = await StargateTools.FindGateById(ID, db);
                                
                                //close remote gate
                                Console.WriteLine("Closing wormhole: " + currentGate.dialed_gate_id);
                                Sessions.SendTo("Impulse:CloseWormhole", currentGate.dialed_gate_id);
                                
                                currentGate.dialed_gate_id = "";
                                currentGate.gate_status = "IDLE";
                                await db.SaveChangesAsync();
                            }
                            
                            break;
                        }
                        
                        //used to update info about the gate on the database
                        case "updateData":
                        {
                            Console.WriteLine("Updated requested");
                            
                            //find gate and update record
                            using (var db = new StargateContext())
                            {
                                var gate = await StargateTools.FindGateById(ID, db);

                                if (gate.id == "NULL")
                                {
                                    Console.WriteLine("No stargate found for update. Is it registered?");
                                    break;
                                }
                                
                                gate.active_users = message.currentUsers;
                                gate.max_users = message.maxUsers;
                                gate.gate_status = message.gate_status;
                                gate.update_date = UnixTimestamp();
                                await db.SaveChangesAsync();
                                
                                Console.WriteLine("Updated record");
                            }
                            
                            break;
                        }
                        
                        //used to update iris state info on the server
                        case "updateIris":
                        {
                            Console.WriteLine("Iris state: " + message.iris_state);
                            
                            using (var db = new StargateContext())
                            {
                                var gate = await StargateTools.FindGateById(ID, db);
                                gate.iris_state = message.iris_state;
                                await db.SaveChangesAsync();
                            }   
                            
                            break;
                        }
                        
                        //keepalive
                        case "keepAlive":
                        {
                            using (var db = new StargateContext())
                            {
                                Stargate gate = await StargateTools.FindGateById(ID, db);
                                gate.update_date = UnixTimestamp();
                                await db.SaveChangesAsync();
                            }
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
            //create stargate database
            

            
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
