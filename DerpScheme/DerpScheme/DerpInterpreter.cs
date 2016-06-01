﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    class Environment
    {
        //track Name/Value pairs, also has heiracrhy
        private Environment parent;
        private Dictionary<string, SExpression> store;

        public Environment(Environment p) { parent = p; store = new Dictionary<string, SExpression>(); }
        public Environment() { store = new Dictionary<string, SExpression>(); }

        public void addVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                throw new Exception(String.Format("Id {0} already exists", id));

            store[id] = val;
        }

        public void setVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                store[id] = val;

            if(parent == null)
                throw new Exception(String.Format("ID {0} does not exist", id));

            parent.setVal(id, val);
        }

        public void setLocalVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                store[id] = val;
            else
                throw new Exception(String.Format("ID {0} does not exist", id));
        }

        public bool hasParent() { return parent != null; }
        public SExpression lookup(SID id) {
            if (store.Keys.Contains(id.identifier))
                return store[id.identifier];

            if (parent != null)
                return parent.lookup(id);
            else
                throw new Exception(String.Format("ID {0} does not exist", id));
        }
    }

    class DerpInterpreter
    {
        Environment e;

        public static void Main()
        {
            DerpInterpreter interp = new DerpInterpreter();

            Console.WriteLine("Welcome to the Derpiter!");
            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                if (input == "exit")
                    return;
                Console.WriteLine(interp.interpret(input));
            }
        }

        public DerpInterpreter() {
            e = new Environment();

            //create and load primitive functions
            Func plus = delegate (List<SExpression> args, Environment e)
            {
                if (args.Count == 0)
                    new SInt(0);

                int rval = 0;
                foreach (SExpression s in args)
                {
                    SExpression tmp = evaluate(s, e);
                    rval += ((SInt)tmp).val;
                }
                return new SInt(rval);
            };
            Func sub = delegate (List<SExpression> args, Environment e)
            {
                if (args.Count == 0)
                    return new SInt(0);
                int rval = ((SInt)evaluate(args[0], e)).val;

                for(int i = 1; i < args.Count; i++)
                {
                    SExpression tmp = evaluate(args[i], e);
                    rval -= ((SInt)tmp).val;
                }
                return new SInt(rval);
            };
            Func mult = delegate (List<SExpression> args, Environment e)
            {
                if (args.Count == 0)
                    return new SInt(1);
                int rval = 1;
                foreach (SExpression s in args)
                {
                    SExpression tmp = evaluate(s, e);
                    rval *= ((SInt)tmp).val;
                }
                return new SInt(rval);
            };
            Func ifExpr = delegate (List<SExpression> args, Environment e)
            {
                SExpression test = evaluate(args[0], e);

                if (args.Count != 3 || !(test is SBool))
                    throw new Exception("Bad args");
                if (((SBool)test).val)
                    return args[1];
                else
                    return args[2];
            };
            Func let = delegate (List<SExpression> args, Environment e)
            {
                Environment local = new Environment(e);
                SList nameBindings = (SList)args[0];
                for(int i=nameBindings.elements.Count - 1; i>0; i--) {
                    String name = ((SID)((SList)nameBindings.elements[i]).head()).identifier;
                    SExpression val = ((SList)nameBindings.elements[i]).body().Last();
                    local.addVal(name, evaluate(val, e));
                }
                return evaluate(args[1], local);
            };
            Func define = delegate(List<SExpression> args, Environment e)
            {
                if (e.hasParent())
                    throw new Exception("Define only allowed at global scope");

                SID name = (SID)args[0];
                SExpression rval = evaluate(args[1], e);
                e.addVal(name.identifier, rval);

                return new SNone();
            };
            Func lambda = delegate (List<SExpression> args, Environment e)
            {
                SList nameList = (SList)args[0];
                SList body = (SList)args[1];
                List<SID> names = new List<SID>();

                for(int i = nameList.elements.Count - 1; i > 0; i--)
                {
                    names.Add((SID)nameList.elements[i]);
                }

                return new SFunc(names, body, e);
            };
            Func div = delegate (List<SExpression> args, Environment e)
            {
                if (args.Count != 2)
                    throw new Exception(String.Format("Expected 2 args, got {0}", args.Count));
                SExpression a = evaluate(args[0], e);
                SExpression b = evaluate(args[1], e);
                int rval = ((SInt)a).val / ((SInt)b).val;
                return new SInt(rval);
            };
            Func mod = delegate (List<SExpression> args, Environment e)
            {
                if (args.Count != 2)
                    throw new Exception(String.Format("Expected 2 args, got {0}", args.Count));
                SExpression a = evaluate(args[0], e);
                SExpression b = evaluate(args[1], e);
                int rval = ((SInt)a).val % ((SInt)b).val;
                return new SInt(rval);
            };

            e.addVal("+", new SPrimitive(plus));
            e.addVal("*", new SPrimitive(mult));
            e.addVal("/", new SPrimitive(div));
            e.addVal("-", new SPrimitive(sub));
            e.addVal("mod", new SPrimitive(mod));
            e.addVal("if", new SPrimitive(ifExpr));
            e.addVal("let", new SPrimitive(let));
            e.addVal("define", new SPrimitive(define));
            e.addVal("lambda", new SPrimitive(lambda));
        }

        public static SExpression evaluate(SExpression expr, Environment e)
        {
            if (expr is SID)
            {
                return e.lookup((SID)expr);
            }

            if (!(expr is SList))
                return expr;

            SList exprL = (SList)expr;

            if (exprL.isEmpty())
                throw new Exception("Can't apply nothing!");
            else if (!exprL.isProperList())
                throw new Exception("Not a proper list!");

            //if head is a SID, lookup val, insure it is callable, then pass control to it
            SExpression head = exprL.head();
            if (head is SID)
                head = e.lookup((SID)head);
            
            if (!(head is SApplicable))
                throw new Exception("SExpression not applicable!");

            //args are going to be body. But because this is a proper list, the last element is going to be a empty list we want to drop
            List<SExpression> args = exprL.body();
            args.RemoveAt(0); //drop empty list
            args.Reverse(); //reverse order so it is fifo for function to deal with

            return ((SApplicable)head).apply(args, e);
        }

        public string interpret(String text)
        {
            try {
                List<Token> tokens = DerpParser.Tokenize(text);
                List<SExpression> ptree = DerpParser.Parse(tokens);

                string res = "";

                foreach (SExpression sxp in ptree)
                    res += DerpInterpreter.evaluate(sxp, e).ToString() + "\n";
                return res;
            }catch(Exception e)
            {
                return "Error: " + e.Message;
            }
        }
    }
}
