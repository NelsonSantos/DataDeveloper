// using System;
// using System.Diagnostics;
// using System.Text;
// using Avalonia.Media;
// using AvaloniaEdit.Document;
// using AvaloniaEdit.Editing;
//
// public static class TextUtilities
// {
//     /// <remarks>
//     /// This function takes a string and converts the whitespace in front of
//     /// it to tabs. If the length of the whitespace at the start of the string
//     /// was not a whole number of tabs then there will still be some spaces just
//     /// before the text starts.
//     /// the output string will be of the form:
//     /// 1. zero or more tabs
//     /// 2. zero or more spaces (less than tabIndent)
//     /// 3. the rest of the line
//     /// </remarks>
//     public static string LeadingWhiteSpaceToTabs(string line, int tabIndent) {
//         StringBuilder _sb = new StringBuilder(line.Length);
//         int _consecutiveSpaces = 0;
//         int _i = 0;
//         for(_i = 0; _i < line.Length; _i++) {
//             if(line[_i] == ' ') {
//                 _consecutiveSpaces++;
//                 if(_consecutiveSpaces == tabIndent) {
//                     _sb.Append('\t');
//                     _consecutiveSpaces = 0;
//                 }
//             }
//             else if(line[_i] == '\t') {
//                 _sb.Append('\t');
//                 // if we had say 3 spaces then a tab and tabIndent was 4 then
//                 // we would want to simply replace all of that with 1 tab
//                 _consecutiveSpaces = 0;					
//             }
//             else {
//                 break;
//             }
//         }
// 		
//         if(_i < line.Length) {
//             _sb.Append(line.Substring(_i-_consecutiveSpaces));
//         }
//         return _sb.ToString();
//     }
// 	
//     public static bool IsLetterDigitOrUnderscore(char c)
//     {
//         if(!Char.IsLetterOrDigit(c)) {
//             return c == '_';
//         }
//         return true;
//     }
// 	
//     public enum CharacterType {
//         LetterDigitOrUnderscore,
//         WhiteSpace,
//         Other
//     }
// 	
//     /// <remarks>
//     /// This method returns the expression before a specified offset.
//     /// That method is used in code completion to determine the expression given
//     /// to the parser for type resolve.
//     /// </remarks>
//     public static string GetExpressionBeforeOffset(TextArea textArea, int initialOffset)
//     {
//         IDocument _document = textArea.Document;
//         int _offset = initialOffset;
//         while (_offset - 1 > 0) {
//             switch (_document.GetCharAt(_offset - 1)) {
//                 case '\n':
//                 case '\r':
//                 case '}':
//                     goto done;
// //						offset = SearchBracketBackward(document, offset - 2, '{','}');
// //						break;
//                 case ']':
//                     _offset = SearchBracketBackward(_document, _offset - 2, '[',']');
//                     break;
//                 case ')':
//                     _offset = SearchBracketBackward(_document, _offset - 2, '(',')');
//                     break;
//                 case '.':
//                     --_offset;
//                     break;
//                 case '"':
//                     if (_offset < initialOffset - 1) {
//                         return null;
//                     }
//                     return "\"\"";
//                 case '\'':
//                     if (_offset < initialOffset - 1) {
//                         return null;
//                     }
//                     return "'a'";
//                 case '>':
//                     if (_document.GetCharAt(_offset - 2) == '-') {
//                         _offset -= 2;
//                         break;
//                     }
//                     goto done;
//                 default:
//                     if (Char.IsWhiteSpace(_document.GetCharAt(_offset - 1))) {
//                         --_offset;
//                         break;
//                     }
//                     int _start = _offset - 1;
//                     if (!IsLetterDigitOrUnderscore(_document.GetCharAt(_start))) {
//                         goto done;
//                     }
// 					
//                     while (_start > 0 && IsLetterDigitOrUnderscore(_document.GetCharAt(_start - 1))) {
//                         --_start;
//                     }
//                     string _word = _document.GetText(_start, _offset - _start).Trim();
//                     switch (_word) {
//                         case "ref":
//                         case "out":
//                         case "in":
//                         case "return":
//                         case "throw":
//                         case "case":
//                             goto done;
//                     }
// 					
//                     if (_word.Length > 0 && !IsLetterDigitOrUnderscore(_word[0])) {
//                         goto done;
//                     }
//                     _offset = _start;
//                     break;
//             }
//         }
//         done:
// //			Console.WriteLine("ofs : {0} cart:{1}", offset, document.Caret.Offset);
// //			Console.WriteLine("return:" + document.GetText(offset, document.Caret.Offset - offset).Trim());
//         //// simple exit fails when : is inside comment line or any other character
//         //// we have to check if we got several ids in resulting line, which usually happens when
//         //// id. is typed on next line after comment one
//         //// Would be better if lexer would parse properly such expressions. However this will cause
//         //// modifications in this area too - to get full comment line and remove it afterwards
//         string _resText=_document.GetText(_offset, textArea.Caret.Offset - _offset ).Trim();
//         int _pos=_resText.LastIndexOf('\n');
//         if (_pos>=0) {
//             _offset+=_pos+1;
//             //// whitespaces and tabs, which might be inside, will be skipped by trim below
//         }							
//         string _expression = _document.GetText(_offset, textArea.Caret.Offset - _offset ).Trim();
//         Console.WriteLine("Expr: >" + _expression + "<");
//         return _expression;
//     }
// 	
// 	
//     public static CharacterType GetCharacterType(char c) 
//     {
//         if(IsLetterDigitOrUnderscore(c))
//             return CharacterType.LetterDigitOrUnderscore;
//         if(Char.IsWhiteSpace(c))
//             return CharacterType.WhiteSpace;
//         return CharacterType.Other;
//     }
// 	
//     public static int GetFirstNonWsChar(IDocument document, int offset)
//     {
//         while (offset < document.TextLength && Char.IsWhiteSpace(document.GetCharAt(offset))) {
//             ++offset;
//         }
//         return offset;
//     }
// 	
//     public static int FindWordEnd(IDocument document, int offset)
//     {
//         var _line   = document.GetLineByOffset(offset);
//         int _endPos = _line.Offset + _line.Length;
//         while (offset < _endPos && IsLetterDigitOrUnderscore(document.GetCharAt(offset))) {
//             ++offset;
//         }
// 		
//         return offset;
//     }
// 	
//     public static int FindWordStart(IDocument document, int offset)
//     {
//         var _line = document.GetLineByOffset(offset);
// 		
//         while (offset > _line.Offset && !IsLetterDigitOrUnderscore(document.GetCharAt(offset - 1))) {
//             --offset;
//         }
// 		
//         return offset;
//     }
// 	
//     // go forward to the start of the next word
//     // if the cursor is at the start or in the middle of a word we move to the end of the word
//     // and then past any whitespace that follows it
//     // if the cursor is at the start or in the middle of some whitespace we move to the start of the
//     // next word
//     public static int FindNextWordStart(IDocument document, int offset)
//     {
//         int _originalOffset = offset;
//         var _line   = document.GetLineByOffset(offset);
//         int     _endPos = _line.Offset + _line.Length;
//         // lets go to the end of the word, whitespace or operator
//         CharacterType _t = GetCharacterType(document.GetCharAt(offset));
//         while (offset < _endPos && GetCharacterType(document.GetCharAt(offset)) == _t) {
//             ++offset;
//         }
// 		
//         // now we're at the end of the word, lets find the start of the next one by skipping whitespace
//         while (offset < _endPos && GetCharacterType(document.GetCharAt(offset)) == CharacterType.WhiteSpace) {
//             ++offset;
//         }
//
//         return offset;
//     }
// 	
//     // go back to the start of the word we are on
//     // if we are already at the start of a word or if we are in whitespace, then go back
//     // to the start of the previous word
//     public static int FindPrevWordStart(IDocument document, int offset)
//     {
//         int _originalOffset = offset;
//         var _line = document.GetLineByOffset(offset);
//         if (offset > 0) {
//             CharacterType _t = GetCharacterType(document.GetCharAt(offset - 1));
//             while (offset > _line.Offset && GetCharacterType(document.GetCharAt(offset - 1)) == _t) {
//                 --offset;
//             }
// 			
//             // if we were in whitespace, and now we're at the end of a word or operator, go back to the beginning of it
//             if(_t == CharacterType.WhiteSpace && offset > _line.Offset) {
//                 _t = GetCharacterType(document.GetCharAt(offset - 1));
//                 while (offset > _line.Offset && GetCharacterType(document.GetCharAt(offset - 1)) == _t) {
//                     --offset;
//                 }
//             }
//         }
// 		
//         return offset;
//     }
// 	
//     public static string GetLineAsString(IDocument document, int lineNumber)
//     {
//         var _line = document.GetLineByNumber(lineNumber);
//         return document.GetText(_line.Offset, _line.Length);
//     }
// 	
//     //[Obsolete("Use IFormattingStrategy.SearchBracketBackward instead.")]
//     public static int SearchBracketBackward(IDocument document, int offset, char openBracket, char closingBracket)
//     {
//         return document..FormattingStrategy.SearchBracketBackward(document, offset, openBracket, closingBracket);
//     }
// 	
//     //[Obsolete("Use IFormattingStrategy.SearchBracketForward instead.")]
//     // public static int SearchBracketForward(IDocument document, int offset, char openBracket, char closingBracket)
//     // {
//     //     return document.FormattingStrategy.SearchBracketForward(document, offset, openBracket, closingBracket);
//     // }
// 	
//     /// <remarks>
//     /// Returns true, if the line lineNumber is empty or filled with whitespaces.
//     /// </remarks>
//     public static bool IsEmptyLine(IDocument document, int lineNumber)
//     {
//         return IsEmptyLine(document, document.GetLineByNumber(lineNumber));
//     }
//
//     /// <remarks>
//     /// Returns true, if the line lineNumber is empty or filled with whitespaces.
//     /// </remarks>
//     public static bool IsEmptyLine(IDocument document, IDocumentLine line)
//     {
//         for (int _i = line.Offset; _i < line.Offset + line.Length; ++_i) {
//             char _ch = document.GetCharAt(_i);
//             if (!Char.IsWhiteSpace(_ch)) {
//                 return false;
//             }
//         }
//         return true;
//     }
// 	
//     static bool IsWordPart(char ch)
//     {
//         return IsLetterDigitOrUnderscore(ch) || ch == '.';
//     }
// 	
//     public static string GetWordAt(IDocument document, int offset)
//     {
//         if (offset < 0 || offset >= document.TextLength - 1 || !IsWordPart(document.GetCharAt(offset))) {
//             return String.Empty;
//         }
//         int _startOffset = offset;
//         int _endOffset   = offset;
//         while (_startOffset > 0 && IsWordPart(document.GetCharAt(_startOffset - 1))) {
//             --_startOffset;
//         }
// 		
//         while (_endOffset < document.TextLength - 1 && IsWordPart(document.GetCharAt(_endOffset + 1))) {
//             ++_endOffset;
//         }
// 		
//         Debug.Assert(_endOffset >= _startOffset);
//         return document.GetText(_startOffset, _endOffset - _startOffset + 1);
//     }
// }