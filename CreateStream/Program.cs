using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreateStream
{
    class Program
    {
        static void Main(string[] args)
        {
            string str = "";

            using (StreamReader sr = new StreamReader(File.OpenRead("dc450_VC_PW.txt")))
            {
                sr.ReadLine(); // читаем первую строку, в которой нет имени
                while (sr.Peek() >= 0)
                {
                    string[] strs = sr.ReadLine().Split('#');
                    if (strs.Length > 6)
                    {
                        HugeLib.TagList tl = new HugeLib.TagList(strs[7].Substring(strs[7].IndexOf(']') + 1));

                        string pin = HugeLib.Crypto.MyCrypto.TripleDES_DecryptData(tl.GetValue("DF20").value, HugeLib.Utils.AHex2Bin("07FB3018A41DDD3E877EE95C75701181"), System.Security.Cryptography.CipherMode.ECB, System.Security.Cryptography.PaddingMode.None);
                        pin = HugeLib.Crypto.MyCrypto.Xor(pin, "0000" + (tl.GetValue("005A").value.Substring(3, 12)));

                        Console.WriteLine($"{tl.GetValue("005A").value}    {pin.Substring(2,4)}");
                    }
                }
                sr.Close();
            }
            //Console.ReadKey();
            return;

            List<string> names = new List<string>();
            using (StreamReader sr = new StreamReader(File.OpenRead("us-500.csv")))
            {
                sr.ReadLine(); // читаем первую строку, в которой нет имени
                while (sr.Peek() >= 0)
                {
                    str = sr.ReadLine();
                    names.Add(str?.Split(',')[0] + " " + str?.Split(',')[1]);
                }
                sr.Close();
            }
            StreamWriter sw = new StreamWriter("alioth.txt", true, Encoding.GetEncoding(1251));
            Random r = new Random(Convert.ToInt32(DateTime.Now.Millisecond));
            for (int i = 0; i < 100; i++)
            {
                string pan = $"419763{i:000000000}";
                #region luhn
                string temp = pan;
                int sum = 0;
                for (int t = 0; t < temp.Length; t++)
                {
                    if (Char.IsDigit(temp[temp.Length - t - 1]))
                    {
                        int p = Convert.ToInt32(temp.Substring(temp.Length - t - 1, 1));
                        if (t % 2 == 0)
                        {
                            p *= 2;
                            p = (p > 9) ? p - 9 : p;
                        }
                        sum += p;
                    }
                }
                sum = (10 - (sum % 10)) % 10;
                pan =  temp + sum.ToString();
                #endregion
                sw.WriteLine($"{i+1:00000}|{pan}|1|{names[i]}|2308|226|||004900||||VC02|603010 нпяй йнийнцн 11 22|ахпяй|01|01||л|19640107|Y|01||2134455544|20090122|нрдекемхел стля пняяхх он яйни накюярх б цнпнде дяйе|||пнлю||VC|||{names[i].Split(' ')[1]}/{names[i].Split(' ')[0]}");
                
            }
            sw.Close();
            return;
            /*
            string[] codes = new string[] {"E9041", "E9422", "E9527", "SP9167", "S7023", "E9323"};
            str = "EMBD007A6770883774511730        151231101210615 SEROV ROMAN                       F            8002                      LME4 9571ENCD00796770883874511730=151222119340579          B6770883874511730^ROMAN/SEROV               ^151222119340579                   CSD10014DRK;                CSD200AAF=СЕРОВ                           ;I=РОМАН                           ;O=АНАТОЛЬЕВИЧ                     ;P=78 04 344597                    ;A=40817810100195099116        CSD40032BRANCH=8002;DELIVER=8008;POSK=;                   CHED0DE0EDTA0DD8A7NOxg";
            StreamWriter sw = new StreamWriter("data.txt", true, Encoding.GetEncoding(1251));
            Random r = new Random(Convert.ToInt32(DateTime.Now.Millisecond));
            for (int i = 0; i < 100; i++)
            {
                sw.WriteLine($"{codes[r.Next(5)]}#{str}");
            }
            sw.Close();
            */
        }
    }
}
