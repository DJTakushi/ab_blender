
using libplctag;
using libplctag.DataTypes;
public enum TagType // TODO : confirm these values
{
    BOOL,
    SINT = 193, //  193, addr, 3, 1 
    INT = 195, // 195, adr, 3,2
    DINT = 196, // expressed by vars with "Dint" in their name
    REAL = 202, // confirmed in the field
    STRING = 33633
}

class TagAttribute // TODO : make an interface and factory to replace stubbing
{
    public required Tag Tag { get; set; }
    public required TagInfo TagInfo { get; set; }
}
