if( args.Count() == 0)
{
    usage();
    return;
}






void usage()
{
    Console.WriteLine("T88Extgractor Path... [-list] [-override]");
    Console.WriteLine("Path: wild card allowed");
    Console.WriteLine("-list: list only");
    Console.WriteLine("-override: erase directory before run");
}
