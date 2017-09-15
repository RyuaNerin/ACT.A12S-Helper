namespace ACT.A12Helper
{
    internal static class IndexOfExt
    {
        public static int IndexOf2(this string str, char value, int sepCount)
        {
            int i = -1;

            while (sepCount-- > 0)
                if ((i = str.IndexOf(value, i + 1)) == -1)
                    return -1;

            return i;
        }
        public static string GetSeparatedPart(this string str, char separator, int index)
        {
            var i = str.IndexOf2(separator, index);
            var l = str.IndexOf(separator, i + 1);
            
            return l != -1 ? str.Substring(i + 1, l - i - 1) : str.Substring(i + 1);
        }
    }
}
