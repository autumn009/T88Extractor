if( args.Count() == 0)
{
    usage();
    return;
}

var listTargets = new List<string>();
bool listFlag = false;
bool overrideFlag = false;
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
foreach (var item in listTargets)
{
    Console.WriteLine(item);
}


void usage()
{
    Console.WriteLine("T88Extgractor Path... [-list] [-override]");
    Console.WriteLine("Path: wild card allowed");
    Console.WriteLine("-list: list only");
    Console.WriteLine("-override: erase directory before run");
}
