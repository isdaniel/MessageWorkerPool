using MessagePack;

[MessagePackObject]
public class TestModle {
    [Key("0")]
    public string Message { get; set; }
}
