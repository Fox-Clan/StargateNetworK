using System.Reflection;
using Bunkum.Core;
using Bunkum.Core.Endpoints;
using Bunkum.Listener.Protocol;
using Bunkum.Protocols.Http;
using Newtonsoft.Json;
using NotEnoughLogs;
using NotEnoughLogs.Behaviour;

public class BunKum
{
    public static async void StartBunKum()
    {
        BunkumServer server = new BunkumHttpServer(new LoggerConfiguration
        { 
            Behaviour = new QueueLoggingBehaviour(),
            #if DEBUG
            MaxLevel = LogLevel.Trace,
            #else
            MaxLevel = LogLevel.Info,
            #endif
        });

        server.Initialize = s =>
        {
            s.DiscoverEndpointsFromAssembly(Assembly.GetExecutingAssembly());
        };
        server.Start();
        await Task.Delay(-1);
    }
}

public class GateEndpoints : EndpointGroup
{
    //returns gatelist for ingame applications
    [HttpEndpoint("/gates", HttpMethods.Get, ContentType.Plaintext)]
    public String GetGates(RequestContext context)
    {
        using (var db = new StargateContext())
        {
            var gates = StargateTools.FindAllGates(db, false);
            string gateList = JsonConvert.SerializeObject(gates);
            return gateList;
        }
    }
}