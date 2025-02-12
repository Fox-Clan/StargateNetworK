using Microsoft.EntityFrameworkCore;


public class StargateTools
{
    public static async Task<Stargate> FindGateByAddress(string address, StargateContext ctx)
    {
        var gate = await ctx.Stargates
            .SingleOrDefaultAsync(b => b.gate_address == address);

        if (gate == null)
        {
            Stargate nullgate = new Stargate()
            {
                id = "NULL"
            };
            return nullgate;
        }
        
        return gate;
    }
    
    public static async Task<Stargate> FindGateById(string id, StargateContext ctx)
    {
        var gate = await ctx.Stargates
            .SingleOrDefaultAsync(b => b.id == id);

        if (gate == null)
        {
            Stargate nullgate = new Stargate()
            {
                id = "NULL"
            };
            return nullgate;
        }
        
        return gate;
    }
    
}
