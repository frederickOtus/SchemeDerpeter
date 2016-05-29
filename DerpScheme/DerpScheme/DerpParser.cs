using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    public enum TokenType { Int, LParen, RParen, Id, Symbol, Bool }

    class Token
    {
        public int col, row;
        public string token;
        public TokenType type;

        public override string ToString()
        {
            return String.Format("({3})R:{0} C:{1} Token:{2}", row, col, token, type);
        }

        public Token(int r, int c, string t)
        {
            row = r; col = c; token = t;
            Regex numReg = new Regex(@"^\d*.?\d+$");
            Match m = numReg.Match(t);

            if (t == "(")
            {
                type = TokenType.LParen;
            } else if (t == ")")
            {
                type = TokenType.RParen;
            }
            else if (m.Success)
            {
                type = TokenType.Int;
            }else if(t == "#t" || t == "#f"){
                type = TokenType.Bool;
            }
            else
            {
                type = TokenType.Id;
            }
        }
    }

    class DerpParser
    {


        public static List<SExpression> parseCode(string text)
        {
            return Parse(Tokenize(text));
        }

        public static List<Token> Tokenize(string text) {
            string toke = ""; int row = 0, col = 0, lrow = 0, lcol = 0;
            List<Token> tokens = new List<Token>();
            string skippedDelims = " \n\r\t";
            string includedDelims = "()";

            for (int i =0; i < text.Length; i++)
            {
                char c = text[i];

                if (includedDelims.Contains(c))
                {
                    if (toke.Length > 0)
                    {
                        tokens.Add(new Token(lrow, lcol, toke));
                    }
                    tokens.Add(new Token(row, col, "" + c));
                    lrow = row; lcol = ++col;
                    toke = "";
                }

                else if (skippedDelims.Contains(c))
                {
                    if (toke.Length > 0)
                    {
                        tokens.Add(new Token(lrow, lcol, toke));
                        toke = "";
                    }

                    if (c == '\n') { row++; col = 0; }
                    else if(c!= '\r'){ col++; }
                    lrow = row; lcol = col;
                }
                else
                {
                    toke += c;
                    col++;
                }
                
            }
            if (toke.Length > 0)
                tokens.Add(new Token(lrow, lcol, toke));

            return tokens;
        }

        public static List<SExpression> Parse(List<Token> tokens)
        {
            List<SExpression> parseTree = new List<SExpression>();
            while(tokens.Count > 0)
            {
                parseTree.Add(ParseSExpression(tokens));
            }
            return parseTree;
        }

        public static SExpression ParseSExpression(List<Token> tokens)
        {
            Token top = tokens.First();
            tokens.RemoveAt(0);
            switch (top.type) {
                case TokenType.Int:
                    return new SInt(Int32.Parse(top.token));
                case TokenType.Id:
                    return new SID(top.token);
                case TokenType.Bool:
                    return new SBool(top.token == "#f");
                case TokenType.LParen:
                    List<SExpression> elms = new List<SExpression>();
                 
                    while (true)
                    {
                        if (tokens.First().type == TokenType.RParen)
                        {
                            elms.Add(new SList());
                            tokens.RemoveAt(0);
                            return new SList(elms);
                        }
                        elms.Add(ParseSExpression(tokens));                       
                    }
                case TokenType.RParen:
                    throw new Exception("Unexpected RParen\n" + top.ToString());
                default:
                    throw new Exception("Unknown Token Type");
            }
        }
    }
}
