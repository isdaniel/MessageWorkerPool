using MessagePack;

[MessagePackObject]
public class TestModle {
    [Key("0")]
    public string Message { get; set; }
    [Key("1")]
    public int Value{ get; set; }
}
