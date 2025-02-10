using System.Net.Sockets;
using System.Security.AccessControl;
using WebSocketSharp;



[SQLite.Table("StarGate")]
public class Stargate
{
    public int active_users { get; set; }
    public string gate_address { get; set; }
    public string gate_code { get; set; }
    public string gate_status { get; set; }
    public string id { get; set; }
    public bool iris_state { get; set; }
    public bool is_headless { get; set; }
    public int max_users { get; set; }
    public string owner_name { get; set; }
    public bool public_gate { get; set; }
    public string session_name { get; set; }
    public string session_url { get; set; }
    public int update_date { get; set; }
    public int creation_date { get; set; }
    public string dialed_gate_id { get; set; }
};



