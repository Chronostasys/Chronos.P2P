using MessagePack;
namespace Chronos.P2P.Test
{
    [MessagePackObject]
    public class Hello
    {
        [Key(0)]
        public string HelloString { get; set; }
    }
}