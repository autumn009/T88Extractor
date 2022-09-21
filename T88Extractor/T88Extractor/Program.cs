using System.Text;

if ( args.Count() == 0)
{
    usage();
    return;
}

var listTargets = new List<string>();
bool listFlag = false;
bool overrideFlag = false;
string workingDirectory = "";
List<string> listIndex = new List<string>();

foreach (var target in args)
{
    if(target == "-list") listFlag = true;
    else if (target == "-override") overrideFlag = true;
    else
    {
        var dir = Path.GetDirectoryName(target)?? Directory.GetCurrentDirectory();
        var name = Path.GetFileName(target);
        foreach (var item in Directory.GetFiles(dir,name))
        {
            listTargets.Add(item);
        }
    }
}
byte[] bytes;
int p = 0;
foreach (var item in listTargets)
{
    Console.WriteLine($"{item}:");
    workingDirectory = Path.Combine(Path.GetDirectoryName(item) ?? ".", Path.GetFileNameWithoutExtension(item));
    if(overrideFlag && Directory.Exists(workingDirectory))
    {
        foreach (var item2 in Directory.EnumerateFiles(workingDirectory))
        {
            File.Delete(item2);
        }
    }
    bytes = File.ReadAllBytes(item);
    p = 0;
    if (checkHeader() == false)
    {
        Console.WriteLine("header not match, Skipped");
        continue;
    }
    var stream = new MemoryStream();
    for (; ; )
    {
        if (p >= bytes.Length) break;
        var tag = getTag();
        if (tag == null) break;
        if (tag is EndTag) break;
        if (tag is DataTag dtag) stream.Write(dtag.Data,12,dtag.Data.Length-12);
    }
    stream.Flush();
    var dbytes = stream.ToArray();
    var dl = new dLoader(dbytes);

    for (; ; )
    {
        bool b = findBasicAndRemove(dl);
        if (!b) break;
    }
    for (; ; )
    {
        bool b = findMonitorAndRemove(dl);
        if (!b) break;
    }
    saveJunk(dl);
    saveIndex();
    Console.WriteLine();
}

int getNextByte()
{
    if (p >= bytes.Length) return -1;
    return bytes[p++];
}

int getNextword()
{
    int l = getNextByte();
    int h = getNextByte();
    return l + h * 256;
}

Tag? getTag()
{
    var id = getNextword();
    var size = getNextword();
    Tag? tag = null;
    if (id == 0) tag = new EndTag();
    else if (id == 1) tag = new VersionTag();
    else if (id == 0x100) tag = new BlankTag();
    else if (id == 0x102) tag = new SpaceTag();
    else if (id == 0x103) tag = new MarkTag();
    else if (id == 0x101)
    {
        var dtag = new DataTag(new byte[size]);
        for (int i = 0; i < size; i++)
        {
            dtag.Data[i] = (byte)getNextByte();
        }
        return dtag;
    }
    else
    {
        Console.WriteLine($"Unknown tag {id:X4}");
        return null;
    }
    p += size;
    return tag;
}

Stream MyCreateOutputStream(string filename)
{
    Directory.CreateDirectory(workingDirectory);
    string fullpath = Path.Combine(workingDirectory, filename);
    if( File.Exists(fullpath) )
    {
        for (int i = 1; ; i++)
        {
            string newFileName = Path.GetFileNameWithoutExtension(filename) + $" ({i})" + Path.GetExtension(filename);
            fullpath = Path.Combine(workingDirectory, newFileName);
            if (!File.Exists(fullpath)) break;
        }
    }
    listIndex.Add(Path.GetFileName(fullpath));
    return File.Create(fullpath);
}


bool checkHeader()
{
    const string header = "PC-8801 Tape Image(T88)\0";
    foreach (var item in header)
    {
        if (item != getNextByte()) return false;
    }
    return true;
}

void saveJunk(dLoader dl)
{
    if (!listFlag)
    {
        using var stream = MyCreateOutputStream($"$JUNK DATA");
        stream.Write(dl.Data);
    }
}

void saveIndex()
{
    if (!listFlag)
    {
        using var writer = File.CreateText(Path.Combine(workingDirectory,$"$Index.txt"));
        foreach (var item in listIndex)
        {
            writer.WriteLine(item);
        }
    }
}

bool findMonitorAndRemove(dLoader dl)
{
    dl.P = 0;
// seek header
tryagain:
    for (; ; )
    {
        dl.StartMark = dl.P;
        int b = dl.getNextByte();
        if (b < 0) goto eof;
        if (b == 0x3a) break;
    }
    var h = dl.getNextByte();
    var l = dl.getNextByte();
    //Console.Write($"&h{h:X2}{l:X2}, ");
    var sumAddr = -(h + l) & 0xff;
    var sum1 = dl.getNextByte();
    if (sumAddr != sum1) goto tryagain;
    Console.Write($"&h{h:X2}{l:X2}, ");

    string errorMessage = "";
    for (; ; )
    {
        var mark = dl.getNextByte();
        if (mark != 0x3a)
        {
            errorMessage = " [NOT DATA HEAD(0x3a) ERR]";
            break;
        }
        var datasize = dl.getNextByte();
        if (datasize == 0) break;
        if (datasize < 0) break;
        int sum = datasize;
        for (int i = 0; i < datasize; i++)
        {
            var d = dl.getNextByte();
            sum += d;
        }
        sum = (-sum) & 0xff;
        var blocksum = dl.getNextByte();
        if (sum != blocksum)
        {
            errorMessage = " [BLOCK CHECKSUM ERR]";
            break;
        }
    }
    dl.EndMark = dl.P;
    if (!listFlag)
    {
        dl.WriteMarkedArea(MyCreateOutputStream($"mon-{h:X2}{l:X2}{errorMessage}.cmt"));
    }
    dl.RemoveMarkedArea();
    return true;
eof:
    return false;
}

bool findBasicAndRemove(dLoader dl)
{
    dl.P = 0;
// seek header
tryagain:
    for (; ; )
    {
        dl.StartMark = dl.P;
        int b = dl.getNextByte();
        if (b < 0) goto eof;
        if (b == 0xd3) break;
    }
    for (int i = 0; i < 9; i++)
    {
        int b = dl.getNextByte();
        if (b < 0) goto eof;
        if (b != 0xd3) goto tryagain;
    }
 
    StringBuilder sb = new StringBuilder();
    for (int i = 0; i < 6; i++)
    {
        int b = dl.getNextByte();
        if (b < 0) goto eof;
        if (b == 0) continue;
        if (b < 0x20 || b >= 0x80 || b == ':' || b == '*' || b == '?') b = 'x';
        sb.Append((char)b);
    }
    string filename = sb.ToString();
    Console.Write($"{filename}, ");

    var stream = new MemoryStream();
    int zeroCount = 0;
    int dataCount = 0;
    for (; ; )
    {
        int b = dl.getNextByte();
        if (b < 0) break;
        if (b == 0) zeroCount++; else zeroCount = 0;
        dataCount++;
        stream.WriteByte((byte)b);
        if (zeroCount == 12)
        {
            //Console.Write($"({dataCount}),");
            break;
        }
    }
    dl.EndMark = dl.P;

    if (!listFlag)
    {
        // 注意! ここはPath.ChangeExtensionを使ってはならない。ファイル名を一部壊す恐れがある
        dl.WriteMarkedArea(MyCreateOutputStream(filename+".cmt"));
    }
    dl.RemoveMarkedArea();
    //dump(dl.Data);
    return true;

eof:;
    return false;
}


void usage()
{
    Console.WriteLine("T88Extgractor Version 1.1");
    Console.WriteLine("T88Extgractor Path... [-list] [-override]");
    Console.WriteLine("Path: wild card allowed");
    Console.WriteLine("-list: list only");
    Console.WriteLine("-override: erase output directory before run");
}

abstract class Tag { }

class EndTag:Tag
{

}

class VersionTag : Tag
{

}

class BlankTag : Tag
{

}

class SpaceTag : Tag
{

}

class MarkTag : Tag
{

}

class DataTag : Tag
{
    public byte[] Data;
    public DataTag(byte[] data)
    {
        Data = data;
    }
}

class dLoader
{
    public byte[] Data;
    public int P = 0;
    public int getNextByte()
    {
        if (P >= Data.Length) return -1;
        return Data[P++];
    }
    public int StartMark = -1;
    public int EndMark = -1;
    public void WriteMarkedArea(Stream stream)
    {
        if (StartMark < 0) throw new Exception("StartMark not set");
        if (EndMark < 0) throw new Exception("EndMark not set");

        using (stream)
        {
            stream.Write(Data,StartMark,EndMark-StartMark);
        }
    }

    public void RemoveMarkedArea()
    {
        if (StartMark < 0) throw new Exception("StartMark not set");
        if (EndMark < 0) throw new Exception("EndMark not set");

        int newsize = Data.Length - (EndMark - StartMark);
        byte[] newdata = new byte[newsize];
        int p = 0;
        for (int i = 0; i < StartMark; i++)
        {
            newdata[p++] = Data[i];
        }
        for (int i = EndMark; i < Data.Length; i++)
        {
            newdata[p++] = Data[i];
        }
        P = 0;
        Data = newdata;
        StartMark = -1;
        EndMark = -1;
    }
    public dLoader(byte[] dBytes)
    {
        Data = dBytes;
    }
}