// $Id: Converter.cs 31 2007-04-14 01:55:50Z mayuki $
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;

namespace Misuzilla.Text.JapaneseStringUtilities
{
	public class Converter
	{
		private static readonly Hashtable _tableZenkakuToHankaku;
		private static readonly Char[] _tableHankakuToZenkakuHanDakuten;
		private static readonly Char[] _tableHankakuToZenkaku;
		private static readonly Char[,] _tableHankakuToZenkakuDakuten;

		private Converter() { }
		
		static Converter()
		{
			_tableZenkakuToHankaku = new Hashtable();
			_tableZenkakuToHankaku['。'] = "｡";
			_tableZenkakuToHankaku['「'] = "｢";
			_tableZenkakuToHankaku['」'] = "｣";
			_tableZenkakuToHankaku['、'] = "､";
			_tableZenkakuToHankaku['・'] = "･";
			_tableZenkakuToHankaku['ヲ'] = "ｦ";
			_tableZenkakuToHankaku['ァ'] = "ｧ";
			_tableZenkakuToHankaku['ィ'] = "ｨ";
			_tableZenkakuToHankaku['ゥ'] = "ｩ";
			_tableZenkakuToHankaku['ェ'] = "ｪ";
			_tableZenkakuToHankaku['ォ'] = "ｫ";
			_tableZenkakuToHankaku['ャ'] = "ｬ";
			_tableZenkakuToHankaku['ュ'] = "ｭ";
			_tableZenkakuToHankaku['ョ'] = "ｮ";
			_tableZenkakuToHankaku['ッ'] = "ｯ";
			_tableZenkakuToHankaku['ー'] = "ｰ";
			_tableZenkakuToHankaku['ア'] = "ｱ";
			_tableZenkakuToHankaku['イ'] = "ｲ";
			_tableZenkakuToHankaku['ウ'] = "ｳ";
			_tableZenkakuToHankaku['エ'] = "ｴ";
			_tableZenkakuToHankaku['オ'] = "ｵ";
			_tableZenkakuToHankaku['カ'] = "ｶ";
			_tableZenkakuToHankaku['キ'] = "ｷ";
			_tableZenkakuToHankaku['ク'] = "ｸ";
			_tableZenkakuToHankaku['ケ'] = "ｹ";
			_tableZenkakuToHankaku['コ'] = "ｺ";
			_tableZenkakuToHankaku['サ'] = "ｻ";
			_tableZenkakuToHankaku['シ'] = "ｼ";
			_tableZenkakuToHankaku['ス'] = "ｽ";
			_tableZenkakuToHankaku['セ'] = "ｾ";
			_tableZenkakuToHankaku['ソ'] = "ｿ";
			_tableZenkakuToHankaku['タ'] = "ﾀ";
			_tableZenkakuToHankaku['チ'] = "ﾁ";
			_tableZenkakuToHankaku['ツ'] = "ﾂ";
			_tableZenkakuToHankaku['テ'] = "ﾃ";
			_tableZenkakuToHankaku['ト'] = "ﾄ";
			_tableZenkakuToHankaku['ナ'] = "ﾅ";
			_tableZenkakuToHankaku['ニ'] = "ﾆ";
			_tableZenkakuToHankaku['ヌ'] = "ﾇ";
			_tableZenkakuToHankaku['ネ'] = "ﾈ";
			_tableZenkakuToHankaku['ノ'] = "ﾉ";
			_tableZenkakuToHankaku['ハ'] = "ﾊ";
			_tableZenkakuToHankaku['ヒ'] = "ﾋ";
			_tableZenkakuToHankaku['フ'] = "ﾌ";
			_tableZenkakuToHankaku['ヘ'] = "ﾍ";
			_tableZenkakuToHankaku['ホ'] = "ﾎ";
			_tableZenkakuToHankaku['マ'] = "ﾏ";
			_tableZenkakuToHankaku['ミ'] = "ﾐ";
			_tableZenkakuToHankaku['ム'] = "ﾑ";
			_tableZenkakuToHankaku['メ'] = "ﾒ";
			_tableZenkakuToHankaku['モ'] = "ﾓ";
			_tableZenkakuToHankaku['ヤ'] = "ﾔ";
			_tableZenkakuToHankaku['ユ'] = "ﾕ";
			_tableZenkakuToHankaku['ヨ'] = "ﾖ";
			_tableZenkakuToHankaku['ワ'] = "ﾜ";
			_tableZenkakuToHankaku['ヲ'] = "ｦ";
			_tableZenkakuToHankaku['ン'] = "ﾝ";
			_tableZenkakuToHankaku['ヴ'] = "ｳﾞ";
			_tableZenkakuToHankaku['゛'] = "ﾞ";
			_tableZenkakuToHankaku['゜'] = "ﾟ";
			_tableZenkakuToHankaku['ガ'] = "ｶﾞ";
			_tableZenkakuToHankaku['ギ'] = "ｷﾞ";
			_tableZenkakuToHankaku['グ'] = "ｸﾞ";
			_tableZenkakuToHankaku['ゲ'] = "ｹﾞ";
			_tableZenkakuToHankaku['ゴ'] = "ｺﾞ";
			_tableZenkakuToHankaku['ザ'] = "ｻﾞ";
			_tableZenkakuToHankaku['ジ'] = "ｼﾞ";
			_tableZenkakuToHankaku['ズ'] = "ｽﾞ";
			_tableZenkakuToHankaku['ゼ'] = "ｾﾞ";
			_tableZenkakuToHankaku['ゾ'] = "ｿﾞ";
			_tableZenkakuToHankaku['ダ'] = "ﾀﾞ";
			_tableZenkakuToHankaku['ヂ'] = "ﾁﾞ";
			_tableZenkakuToHankaku['ヅ'] = "ﾂﾞ";
			_tableZenkakuToHankaku['デ'] = "ﾃﾞ";
			_tableZenkakuToHankaku['ド'] = "ﾄﾞ";
			_tableZenkakuToHankaku['バ'] = "ﾊﾞ";
			_tableZenkakuToHankaku['ビ'] = "ﾋﾞ";
			_tableZenkakuToHankaku['ブ'] = "ﾌﾞ";
			_tableZenkakuToHankaku['ベ'] = "ﾍﾞ";
			_tableZenkakuToHankaku['ボ'] = "ﾎﾞ";
			_tableZenkakuToHankaku['パ'] = "ﾊﾟ";
			_tableZenkakuToHankaku['ピ'] = "ﾋﾟ";
			_tableZenkakuToHankaku['プ'] = "ﾌﾟ";
			_tableZenkakuToHankaku['ペ'] = "ﾍﾟ";
			_tableZenkakuToHankaku['ポ'] = "ﾎﾟ";
			
			_tableHankakuToZenkaku = new Char[] {
				'。', '「', '」', '、', '・', 'ヲ',
				'ァ', 'ィ', 'ゥ', 'ェ', 'ォ',
				'ャ', 'ュ', 'ョ', 'ッ', 'ー',
				'ア', 'イ', 'ウ', 'エ', 'オ',
				'カ', 'キ', 'ク', 'ケ', 'コ',
				'サ', 'シ', 'ス', 'セ', 'ソ',
				'タ', 'チ', 'ツ', 'テ', 'ト',
				'ナ', 'ニ', 'ヌ', 'ネ', 'ノ',
				'ハ', 'ヒ', 'フ', 'ヘ', 'ホ',
				'マ', 'ミ', 'ム', 'メ', 'モ',
				'ヤ', 'ユ', 'ヨ',
				'ラ', 'リ', 'ル', 'レ', 'ロ',
				'ワ', 'ン',
				'゛', '゜',
			};
			_tableHankakuToZenkakuDakuten = new Char[,] {
				{' ', ' ', ' ', ' ', ' '},
				{'ガ', 'ギ', 'グ', 'ゲ', 'ゴ'},
				{'ザ', 'ジ', 'ズ', 'ゼ', 'ゾ'},
				{'ダ', 'ヂ', 'ヅ', 'デ', 'ド'},
				{' ', ' ', ' ', ' ', ' '},
				{'バ', 'ビ', 'ブ', 'ベ', 'ボ'},
			};
			_tableHankakuToZenkakuHanDakuten = new Char[] {
				'パ', 'ピ', 'プ', 'ペ', 'ポ',
			};
		}
		
		public static String
		Convert(String str, ConvertFlags wideFlag, ConvertFlags narrowFlag)
		{
			StringBuilder sb = new StringBuilder();
			//Console.WriteLine("Convert In: {0}", str);
			for (Int32 i = 0; i < str.Length; i++) {
				Char c = str[i];
				Boolean isNextDakuten = (str.Length > i+1 ? (str[i+1] == 'ﾞ') : false);
				Boolean isNextHanDakuten = (str.Length > i+1 ? (str[i+1] == 'ﾟ') : false);
				//Console.WriteLine("  - char: {0}", c);
				//Console.WriteLine("  - isNextDakuten: {0}", isNextDakuten);
				//Console.WriteLine("  - isNextHanDakuten: {0}", isNextHanDakuten);
				
				if (((narrowFlag & ConvertFlags.Katakana) != 0) && _tableZenkakuToHankaku.ContainsKey(c)) {
					// 全角カナ -> 半角カナ
					sb.Append(_tableZenkakuToHankaku[c]);
				} else if (((wideFlag & ConvertFlags.Katakana) != 0) && (c >= '｡' && c <= 'ﾟ')) {
					// 半角カナ -> 全角カナ
					Int32 col = (c - 'ｱ') / 5; // アカサタナ行
					Int32 row = (c - 'ｱ') % 5; // アイウエオ
					//Console.WriteLine("    - char: {0} at {1} - {2}", c, col, row);
					
					if (isNextDakuten) {
						switch (col) {
						case 1: case 2: case 3: case 5:
							//Console.WriteLine("      -> {0}", _tableHankakuToZenkakuDakuten[col, row]);
							sb.Append(_tableHankakuToZenkakuDakuten[col, row]);
							i++;
							break;
						default:
							if (c == 'ｳ') {
								sb.Append('ヴ');
								i++;
							}
							break;
						}
					} else if (isNextHanDakuten && col == 5) {
						sb.Append(_tableHankakuToZenkakuHanDakuten[row]);
						i++;
					} else {
						//Console.WriteLine("      -> {0}", _tableHankakuToZenkaku[(c - '｡')]);
						sb.Append(_tableHankakuToZenkaku[(c - '｡')]);
					}
				} else if (((wideFlag & ConvertFlags.Alphabet) != 0) && (c >= '!' && c <= '~' && (c < '0' || c > '9'))) {
					// 半角アルファベット -> 全角アルファベット
					sb.Append((Char)('！' + (c - '!')));
				} else if (((narrowFlag & ConvertFlags.Alphabet) != 0) && (c >= '！' && c <= '～' && (c < '０' || c > '９'))) {
					// 全角アルファベット -> 半角アルファベット
					sb.Append((Char)('!' + (c - '！')));
				} else if (((wideFlag & ConvertFlags.Numeric) != 0) && (c >= '0' && c <= '9')) {
					// 半角数字 -> 全角数字
					sb.Append((Char)('０' + (c - '0')));
				} else if (((narrowFlag & ConvertFlags.Numeric) != 0) && (c >= '０' && c <= '９')) {
					// 全角数字 -> 半角数字
					sb.Append((Char)('0' + (c - '０')));
				} else if (((wideFlag & ConvertFlags.Space) != 0) && (c == ' ')) {
					// 半角空白 -> 全角空白
					sb.Append('　');
				} else if (((narrowFlag & ConvertFlags.Space) != 0) && (c == '　')) {
					// 全角空白 -> 半角空白
					sb.Append(' ');
				} else {
					sb.Append(c);
				}
			}
			return sb.ToString();
		}
	}
	[Flags]
	public enum ConvertFlags
	{
		None         = 0x0000,
		Katakana     = 0x0001,
		Numeric      = 0x0002,
		Alphabet     = 0x0004,
		AlphaNumeric = Numeric | Alphabet,
		Space        = 0x0008,
		All          = Katakana | AlphaNumeric | Space
	}
}