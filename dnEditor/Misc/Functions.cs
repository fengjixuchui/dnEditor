﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using dnEditor.Handlers;
using dnEditor.Properties;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace dnEditor.Misc
{
    public static class Functions
    {
        public static Dictionary<OpCode, string> OpCodeDictionary;
        public static List<OpCode> OpCodes;

        static Functions()
        {
            LoadOpCodes();
            UpdateOpCodeDictionary();
        }

        public static string GetOpCodeDefinition(string opCode)
        {
            OpCode result = GetOpCode(opCode);

            if (result == null) return null;

            string definition;
            OpCodeDictionary.TryGetValue(result, out definition);

            return definition;
        }

        public static string GetOpCodeDefinition(OpCode opCode)
        {
            string definition;
            OpCodeDictionary.TryGetValue(opCode, out definition);

            return definition;
        }

        public static OpCode GetOpCode(string opCode)
        {
            OpCode result = OpCodes.FirstOrDefault(opcode => opcode.Name == opCode);
            return result;
        }

        public static void UpdateOpCodeDictionary()
        {
            if (OpCodeDictionary == null)
                OpCodeDictionary = new Dictionary<OpCode, string>();
            else
                OpCodeDictionary.Clear();

            string[] dictionary = Regex.Split(Resources.MSIL_Dictionary, Environment.NewLine);

            foreach (string line in dictionary)
            {
                string[] items = Regex.Split(line, "=");
                OpCode result = OpCodes.FirstOrDefault(opCode => opCode.Name.ToLower() == items[0].ToLower());
                if (result != null)
                    OpCodeDictionary.Add(result, items[1]);
            }
        }

        public static void LoadOpCodes()
        {
            if (OpCodes == null)
                OpCodes = new List<OpCode>();
            else
                OpCodes.Clear();

            Type type = typeof (OpCodes);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (FieldInfo field in fields)
            {
                if (field.FieldType.Name != "OpCode") continue;
                var opCode =
                    (OpCode)
                        type.InvokeMember(field.Name, BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField,
                            null, null, null);
                OpCodes.Add(opCode);
            }

            OpCodes = OpCodes.OrderBy(o => o.Name).ToList();
        }

        public static string GetAddress(Instruction instruction)
        {
            return String.Format("L_{0:x04}", instruction.Offset);
        }

        public static string GetAddress(uint offset)
        {
            return String.Format("L_{0:x04}", offset);
        }

        public static string FormatFullInstruction(List<Instruction> instructions, int index)
        {
            if (index < 0) return "???";

            Instruction currentInstruction = instructions[index];

            string output = String.Format("({0}) {1}", instructions.IndexOf(currentInstruction),
                currentInstruction.OpCode);

            if (currentInstruction.Operand != null)
            {
                if (currentInstruction.Operand is Instruction)
                {
                    output += String.Format("{0}", ResolveOperandInstructions(instructions, index));
                }
                else
                {
                    output += String.Format(": {0}", GetOperandText(instructions, index));
                }
            }

            return output;
        }

        public static string FormatInstruction(List<Instruction> instructions, int index)
        {
            if (index < 0) return "???";

            Instruction currentInstruction = instructions[index];

            string output = String.Format("({0}) {1}", instructions.IndexOf(currentInstruction),
                currentInstruction.OpCode);

            if (currentInstruction.Operand != null && (!(currentInstruction.Operand is Instruction)))
            {
                output += String.Format(": {0}", GetOperandText(instructions, index));
            }

            return output;
        }

        private static string ResolveOperandInstructions(List<Instruction> instructions, int index)
        {
            string output = "";
            Instruction currentInstruction = instructions[index];

            int repeats = 0;
            while (currentInstruction.Operand is Instruction &&
                   (currentInstruction.Operand as Instruction) != currentInstruction && repeats < 5)
            {
                var newInstruction = currentInstruction.Operand as Instruction;
                output += String.Format(" -> {0}", FormatInstruction(instructions, instructions.IndexOf(newInstruction)));
                currentInstruction = newInstruction;

                repeats++;
            }

            return output;
        }

        public static string GetOperandText(List<Instruction> instructions, int index)
        {
            string operandText = "";

            object operand = instructions[index].Operand;

            if (operand == null)
            {
                return operandText;
            }

            switch (operand.GetType().FullName)
            {
                case "System.String":
                    operandText = String.Format("\"{0}\"", operand);
                    break;
                case "System.Int32":
                case "System.Int16":
                case "System.Int64":
                    long l = Convert.ToInt64(operand);
                    operandText = l < 100 ? l.ToString() : String.Format("0x{0:x}", l);
                    break;
                case "System.UInt32":
                case "System.UInt16":
                case "System.UInt64":
                    ulong ul = Convert.ToUInt64(operand);
                    operandText = ul < 100 ? ul.ToString() : String.Format("0x{0:x}", ul);
                    break;
                case "System.Decimal":
                    operandText = operand.ToString();
                    break;
                case "System.Double":
                    operandText = operand.ToString();
                    break;
                case "System.Byte":
                case "System.SByte":
                    operandText = String.Format("0x{0:x}", operand);
                    break;
                case "System.Boolean":
                    operandText = operand.ToString();
                    break;
                case "System.Char":
                    operandText = String.Format("'{0}'", operand);
                    break;
                case "System.DateTime":
                    operandText = operand.ToString();
                    break;
                case "dnlib.DotNet.Emit.Instruction[]":
                    operandText = GetSwitchText(instructions, (instructions[index].Operand as Instruction[]).ToList());
                    break;
                default:
                    operandText = operand.ToString();
                    break;
            }
            return operandText.ShortenOperandText();
        }

        public static string GetSwitchText(List<Instruction> instructions, List<Instruction> switchInstructions)
        {
            var stringBuilder = new StringBuilder();

            if (switchInstructions.Count > 0)
            {
                foreach (Instruction instruction in switchInstructions)
                {
                    stringBuilder.AppendFormat("({0}) {1}, ", instructions.IndexOf(instruction), instruction.OpCode);
                }

                stringBuilder.Remove(stringBuilder.Length - 2, 2);
            }

            return stringBuilder.ToString();
        }

        public static object GetItemByText(this ComboBox comboBox, string text)
        {
            return comboBox.Items.Cast<object>().First(item => item.ToString() == text);
        }

        public static bool IsExpandable(this TypeDef type)
        {
            return (type.HasNestedTypes || type.HasMethods || type.HasEvents || type.HasFields || type.HasProperties);
        }

        public static TreeNode FindMethod(TreeNode node, MethodDef method)
        {
            foreach (TreeNode subNode in node.Nodes)
            {
                if ((subNode.Tag is MethodDef) && (subNode.Tag as MethodDef) == method)
                {
                    return subNode;
                }

                TreeNode nodeResult = FindMethod(subNode, method);

                if (nodeResult != null)
                    return nodeResult;
            }
            return null;
        }

        public static bool OpenFile(TreeViewHandler treeViewHandler, string file, ref CurrentAssembly currentAssembly,
            bool clear = false)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentException("No path provided!");

            var newCurrentAssembly = new CurrentAssembly(file);

            if (newCurrentAssembly.Assembly == null) return false;
            currentAssembly = newCurrentAssembly;

            treeViewHandler.LoadAssembly(currentAssembly.Assembly, file, clear);
            return true;
        }

        public static TreeNode FirstParentNode(this TreeNode node)
        {
            while (node.Parent != null)
                node = node.Parent;

            return node;
        }
    }
}