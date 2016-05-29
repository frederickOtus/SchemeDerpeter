using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DerpScheme
{
    class Environment
    {

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
                throw new Exception("Bad id!");

            parent.setVal(id, val);
        }
        public void setLocalVal(string id, SExpression val)
        {
            if (store.Keys.Contains(id))
                store[id] = val;
            else
                throw new Exception("Bad id!");
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
            String text = "";
            try
            {   // Open the text file using a stream reader.
                using (StreamReader sr = new StreamReader("SampleCode/test.sc"))
                {
                    // Read the stream to a string, and write the string to the console.
                    text = sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }

            List<Token> tokens = DerpParser.Tokenize(text);
            List<SExpression> ptree = DerpParser.Parse(tokens);

            foreach (SExpression s in ptree)
            {
                Console.WriteLine(s);
            }

            DerpInterpreter ci = new DerpInterpreter();
            foreach (SExpression sxp in ptree)
                Console.WriteLine(ci.interpret(sxp));
        }

        public DerpInterpreter() {



            e = new Environment();

            //create and load primitive functions
            Func plus = delegate (List<SExpression> args, Environment e)
            {
                int rval = 0;
                foreach (SExpression s in args)
                {
                    SExpression tmp = evaluate(s, e);
                    rval += ((SInt)tmp).val;
                }
                return new SInt(rval);
            };
            Func mult = delegate (List<SExpression> args, Environment e)
            {
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

            e.addVal("+", new SPrimitive(plus));
            e.addVal("*", new SPrimitive(mult));
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

        public string interpret(SExpression expr)
        {
            return evaluate(expr, e).ToString();
        }

    }
}
