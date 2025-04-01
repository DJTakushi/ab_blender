using libplctag;
using libplctag.DataTypes;

class TagAttribute(TagInfo tagInfo, string address, PlcType plc_type, Protocol plc_protocol) : ITagAttribute
{
    public TagInfo TagInfo = tagInfo;
    private readonly Tag Tag = new()
    {
        Name = $"{tagInfo.Name}",
        Path = "1,0", // assuming default,
        Gateway = address,
        PlcType = plc_type,
        Protocol = plc_protocol
    };
    public virtual TagType GetTagType()
    {
        return (TagType)TagInfo.Type;
    }
    public virtual string GetTagName()
    {
        return TagInfo.Name;
    }
    public virtual void InitializeTag()
    {
        Tag.Initialize();
    }
    public virtual void ReadTag()
    {
        Tag.Read();
    }
    public virtual double GetDoubleTagValue(int offset = 0)
    {
        return Tag.GetFloat32(offset);
    }
    public virtual bool GetBoolTagValue(int offset = 0)
    {
        return Tag.GetBit(offset);
    }
    public virtual int GetSintTagValue(int offset = 0)
    {
        return Tag.GetInt8(offset);
    }
    public virtual int GetIntTagValue(int offset = 0)
    {
        return Tag.GetInt16(offset);
    }
    public virtual int GetDintTagValue(int offset = 0)
    {
        return Tag.GetInt32(offset);
    }
    public virtual string GetStringTagValue(int offset = 0)
    {
        return Tag.GetString(offset);
    }
}
