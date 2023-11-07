﻿/* GerberAperture.cs - Classes for handling gerber apertures. */

/*  Copyright (C) 2015-2021 Milton Neal <milton200954@gmail.com>
    *** Acknowledgments to Gerbv Authors and Contributors. ***

    Redistribution and use in source and binary forms, with or without
    modification, are permitted provided that the following conditions
    are met:

    1. Redistributions of source code must retain the above copyright
       notice, this list of conditions and the following disclaimer.
    2. Redistributions in binary form must reproduce the above copyright
       notice, this list of conditions and the following disclaimer in the
       documentation and/or other materials provided with the distribution.
    3. Neither the name of the project nor the names of its contributors
       may be used to endorse or promote products derived from this software
       without specific prior written permission.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
    ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
    IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
    ARE DISCLAIMED.  IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
    FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
    DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
    OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
    HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
    LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
    OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
    SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;

namespace GerberVS
{
    /// <summary>
    /// Type class containing information on an aperture for stats report.
    /// </summary>
    public class GerberApertureInfo
    {
        private readonly double[] parameters;

        /// <summary>
        /// Aperture number.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Aperture level.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Number of D codes.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Type of aperture.
        /// </summary>
        public GerberApertureType ApertureType { get; set; }

        /// <summary>
        /// Creates a new instance of the aperture information type class.
        /// </summary>
        public GerberApertureInfo()
        {
            parameters = new double[5];
        }

        /// <summary>
        /// Aperture parameter list.
        /// </summary>
        public double[] Parameters()
        {
            return parameters;
        }
    }

    /// <summary>
    /// Type class for defining an aperture.
    /// </summary>
    public class Aperture
    {
        private readonly double[] parameters;

        /// <summary>
        /// Aperture type.
        /// </summary>
        public GerberApertureType ApertureType { get; set; }

        /// <summary>
        /// Aperture macro.
        /// </summary>
        public ApertureMacro ApertureMacro { get; set; }

        /// <summary>
        /// Actual number of parameters.
        /// </summary>
        public int ParameterCount { get; set; }

        /// <summary>
        /// Scale units of the parameters.
        /// </summary>
        public GerberUnit Unit { get; set; }

        /// <summary>
        /// Gets the simplified macro list.
        /// </summary>
        public Collection<SimplifiedApertureMacro> SimplifiedMacroList { get; }

        /// <summary>
        /// Creates a new instance of an aperture definition type class.
        /// </summary>
        public Aperture()
        {
            SimplifiedMacroList = new Collection<SimplifiedApertureMacro>();
            parameters = new double[Gerber.MaximumApertureParameters];
        }

        /// <summary>
        /// Aperture parameters.
        /// </summary>
        public double[] Parameters()
        {
            return parameters;
        }
    }

    /// <summary>
    /// Type class for defining an aperture macro.
    /// </summary>
    public class ApertureMacro
    {
        /// <summary>
        /// Gets the macro instruction list.
        /// </summary>
        public List<GerberInstruction> InstructionList { get; }

        /// <summary>
        /// Aperture name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Nuf pushes in program to estimate stack size.
        /// </summary>
        public int NufPushes { get; set; }

        /// <summary>
        /// Creates a new instance of the Aperture Macro class.
        /// </summary>
        public ApertureMacro()
        {
            InstructionList = new List<GerberInstruction>();
        }

        /// <summary>
        /// Read in and resolve the aperture macro data.
        /// </summary>
        /// <param name="reader">line holding the macro data</param>
        /// <returns>the resolved apertur macro</returns>
        public static ApertureMacro ParseApertureMacro(GerberLineReader reader)
        {
            const int MathOperationStackSize = 2;

            ApertureMacro apertureMacro = new ApertureMacro();
            GerberOpCode[] mathOperations = new GerberOpCode[MathOperationStackSize];
            GerberInstruction instruction;
            
            char characterRead;
            int primitive = 0;
            int mathOperationIndex = 0;
            int equate = 0;
            bool done = false;
            bool comma = false;                // Just read an operator (one of '*+X/)
            bool isNegative = false;           // Negative numbers succeeding ','
            bool foundPrimitive = false;
            int length = 0;

            // Get macro name
            apertureMacro.Name = reader.ReadLine('*');
            characterRead = reader.Read();	    // Skip '*'

            // The first instruction in all programs will be NOP.
            instruction = new GerberInstruction();
            instruction.Opcode = GerberOpCode.Nop;
            apertureMacro.InstructionList.Add(instruction);

            while (!done && !reader.EndOfFile)
            {
                length = 0;
                characterRead = reader.Read();
                switch (characterRead)
                {
                    case '$':
                        if (foundPrimitive)
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = GerberOpCode.PushParameter;
                            apertureMacro.InstructionList.Add(instruction);
                            apertureMacro.NufPushes++;
                            instruction.Data.IntValue = reader.GetIntegerValue(ref length);
                            comma = false;
                        }

                        else
                            equate = reader.GetIntegerValue(ref length);

                        break;

                    case '*':
                        while (mathOperationIndex != 0)
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        // Check is due to some gerber files has spurious empty lines (eg EagleCad)
                        if (foundPrimitive)
                        {
                            instruction = new GerberInstruction();
                            if (equate > 0)
                            {
                                instruction.Opcode = GerberOpCode.PopParameter;
                                instruction.Data.IntValue = equate;
                            }

                            else
                            {
                                instruction.Opcode = GerberOpCode.Primitive;
                                instruction.Data.IntValue = primitive;
                            }

                            apertureMacro.InstructionList.Add(instruction);

                            equate = 0;
                            primitive = 0;
                            foundPrimitive = false;
                        }
                        break;

                    case '=':
                        if (equate > 0)
                            foundPrimitive = true;

                        break;

                    case ',':
                        if (!foundPrimitive)
                        {
                            foundPrimitive = true;
                            break;
                        }

                        while (mathOperationIndex != 0)
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        comma = true;
                        break;

                    case '+':
                        while ((mathOperationIndex != 0) &&
                                OperatorPrecedence((mathOperationIndex > 0) ? mathOperations[mathOperationIndex - 1] : GerberOpCode.Nop) >=
                                (OperatorPrecedence(GerberOpCode.Add)))
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        mathOperations[mathOperationIndex++] = GerberOpCode.Add;
                        comma = true;
                        break;

                    case '-':
                        if (comma)
                        {
                            isNegative = true;
                            comma = false;
                            break;
                        }

                        while ((mathOperationIndex != 0) &&
                                OperatorPrecedence((mathOperationIndex > 0) ? mathOperations[mathOperationIndex - 1] : GerberOpCode.Nop) >=
                                (OperatorPrecedence(GerberOpCode.Subtract)))
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        mathOperations[mathOperationIndex++] = GerberOpCode.Subtract;
                        break;

                    case '/':
                        while ((mathOperationIndex != 0) &&
                                OperatorPrecedence((mathOperationIndex > 0) ? mathOperations[mathOperationIndex - 1] : GerberOpCode.Nop) >=
                                (OperatorPrecedence(GerberOpCode.Divide)))
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        mathOperations[mathOperationIndex++] = GerberOpCode.Divide;
                        comma = true;
                        break;

                    case 'X':
                    case 'x':
                        while ((mathOperationIndex != 0) &&
                            OperatorPrecedence((mathOperationIndex > 0) ? mathOperations[mathOperationIndex - 1] : GerberOpCode.Nop) >=
                            (OperatorPrecedence(GerberOpCode.Multiple)))
                        {
                            instruction = new GerberInstruction();
                            instruction.Opcode = mathOperations[--mathOperationIndex];
                            apertureMacro.InstructionList.Add(instruction);
                        }

                        mathOperations[mathOperationIndex++] = GerberOpCode.Multiple;
                        comma = true;
                        break;

                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '.':
                        // Comments in aperture macros are a definition starting with zero and ends with a '*'
                        if ((characterRead == '0') && (!foundPrimitive) && (primitive == 0))
                        {
                            // Comment continues until next '*', just throw it away.
                            reader.ReadLine('*');
                            characterRead = reader.Read(); // Read the '*'.
                            break;
                        }

                        // First number in an aperture macro describes the primitive as a numerical value
                        if (!foundPrimitive)
                        {
                            primitive = (primitive * 10) + (characterRead - '0');
                            break;
                        }

                        reader.Position--;
                        instruction = new GerberInstruction();
                        instruction.Opcode = GerberOpCode.Push;
                        apertureMacro.InstructionList.Add(instruction);
                        apertureMacro.NufPushes++;
                        instruction.Data.DoubleValue = reader.GetDoubleValue();
                        if (isNegative)
                            instruction.Data.DoubleValue = -(instruction.Data.DoubleValue);

                        isNegative = false;
                        comma = false;
                        break;

                    case '%':
                        reader.Position--;      // Must return with % first in string since the main parser needs it.
                        done = true;            // Reached the end of the macro.
                        break;

                    default:
                        // Whitespace.
                        break;
                }
            }

            if (reader.EndOfFile)
                return null;

            return apertureMacro;
        }

        /// <summary>
        /// Gets the precedence of operator codes.
        /// </summary>
        /// <param name="opcode">operater code</param>
        /// <returns>value indicating precedence</returns>
        private static int OperatorPrecedence(GerberOpCode opcode)
        {
            switch (opcode)
            {
                case GerberOpCode.Add:
                case GerberOpCode.Subtract:
                    return 1;

                case GerberOpCode.Multiple:
                case GerberOpCode.Divide:
                    return 2;
            }

            return 0;
        }
    }

    /// <summary>
    /// Type class for simplifing and defining an aperture macro.
    /// </summary>
    public class SimplifiedApertureMacro
    {
        /// <summary>
        /// Aperture type.
        /// </summary>
        public GerberApertureType ApertureType { get; set; }

        /// <summary>
        /// Parameters that define the aperture.
        /// </summary>
        public Collection<double> Parameters { get; }

        /// <summary>
        /// Creates a new instance of a aperture macro.
        /// </summary>
        public SimplifiedApertureMacro()
        {
            Parameters = new Collection<double>();
        }

        /// <summary>
        /// Creates a new instance of a aperture macro with predefined parameters.
        /// </summary>
        /// <param name="parameters"></param>
        public SimplifiedApertureMacro(Collection<double> parameters)
        {
            Parameters = parameters;
        }

        /// <summary>
        /// Simplifies an aperture macro.
        /// </summary>
        /// <param name="aperture">aperture to be simplified</param>
        /// <param name="scale">scale to use when simplifing</param>
        /// <returns> true on success</returns>
        public static bool SimplifyApertureMacro(Aperture aperture, double scale)
        {
            const int extraStackSize = 10;

            bool success = true;
            int numberOfParameters = 0;
            bool clearOperatorUsed = false;
            double[] temp = { 0.0, 0.0 };
            GerberApertureType type = GerberApertureType.None;
            SimplifiedApertureMacro macro;

            if (aperture == null || aperture.ApertureMacro == null)
                throw new GerberApertureException("In SimplifyApertureMacro, aperture = null");

            // Allocate stack.
            MacroStack.InitializeStack(aperture.ApertureMacro.NufPushes + extraStackSize);

            foreach (GerberInstruction instruction in aperture.ApertureMacro.InstructionList)
            {
                switch (instruction.Opcode)
                {
                    case GerberOpCode.Nop:
                        break;

                    case GerberOpCode.Push:
                        MacroStack.Push(instruction.Data.DoubleValue);
                        break;

                    case GerberOpCode.PushParameter:
                        MacroStack.Push(aperture.Parameters()[instruction.Data.IntValue - 1]);
                        break;

                    case GerberOpCode.PopParameter:
                        temp[0] = MacroStack.Pop();
                        aperture.Parameters()[instruction.Data.IntValue - 1] = temp[0];
                        break;

                    case GerberOpCode.Add:
                        temp[0] = MacroStack.Pop();
                        temp[1] = MacroStack.Pop();
                        MacroStack.Push(temp[1] + temp[0]);
                        break;

                    case GerberOpCode.Subtract:
                        temp[0] = MacroStack.Pop();
                        temp[1] = MacroStack.Pop();
                        MacroStack.Push(temp[1] - temp[0]);
                        break;

                    case GerberOpCode.Multiple:
                        temp[0] = MacroStack.Pop();
                        temp[1] = MacroStack.Pop();
                        MacroStack.Push(temp[1] * temp[0]);
                        break;

                    case GerberOpCode.Divide:
                        temp[0] = MacroStack.Pop();
                        temp[1] = MacroStack.Pop();
                        MacroStack.Push(temp[1] / temp[0]);
                        break;

                    case GerberOpCode.Primitive:
                        // This handles the exposure thing in the aperture macro.
                        // The exposure is always the first element on stack independent
                        // of aperture macro.
                        switch (instruction.Data.IntValue)
                        {
                            case 1:
                                //Debug.Write("    Aperture macro circle [1] (");
                                type = GerberApertureType.MacroCircle;
                                numberOfParameters = 4;
                                break;

                            case 3:
                                break;  // EOF.

                            case 4:
                                //Debug.Write("    Aperture macro outline [4] (");
                                type = GerberApertureType.MacroOutline;
                                // Number of parameters are:
                                // Number of points defined in entry 1 of the stack + start point. Times two since it is both X and Y.
                                // Then three more; exposure, nuf points and rotation.
                                numberOfParameters = ((int)MacroStack.Values[1] + 1) * 2 + 3;
                                if(numberOfParameters < 0 || numberOfParameters >= int.MaxValue / 4)
                                {
                                    throw new GerberApertureException("Invalid number of parameters for an Outline aperture macro.");
                                }

                                break;

                            case 5:
                                //Debug.Write("    Aperture macro polygon [5] (");
                                type = GerberApertureType.MacroPolygon;
                                numberOfParameters = 6;
                                break;

                            case 6:
                                //Debug.WriteLine("    Aperture macro moiré [6] (");
                                type = GerberApertureType.MacroMoire;
                                numberOfParameters = 9;
                                break;

                            case 7:
                                //Debug.Write("    Aperture macro thermal [7] (");
                                type = GerberApertureType.MacroThermal;
                                numberOfParameters = 6;
                                break;

                            case 2:
                            case 20:
                                //Debug.Write("    Aperture macro line 20/2 (");
                                type = GerberApertureType.MacroLine20;
                                numberOfParameters = 7;
                                break;

                            case 21:
                                //Debug.Write("    Aperture macro line 21 (");
                                type = GerberApertureType.MacroLine21;
                                numberOfParameters = 6;
                                break;

                            case 22:
                                //Debug.Write("    Aperture macro line 22 (");
                                type = GerberApertureType.MacroLine22;
                                numberOfParameters = 6;
                                break;

                            default:
                                success = false;
                                break;
                        }

                        if (type != GerberApertureType.None)
                        {
                            if (numberOfParameters > Gerber.MaximumApertureParameters)
                            {
                                throw new GerberApertureException("Too many parameters for aperture macro;");
                                // GERB_COMPILE_ERROR("Number of parameters to aperture macro are more than gerbv is able to store\n");
                            }

                            // Create a new simplified aperture macro and start filling in the blanks.
                            macro = new SimplifiedApertureMacro();
                            macro.ApertureType = type;
                            for (int i = 0; i < numberOfParameters; i++)
                            {
                                macro.Parameters.Add(MacroStack.Values[i]);
                            }

                            // Convert any mm values to inches.
                            switch (type)
                            {
                                case GerberApertureType.MacroCircle:
                                    if (Math.Abs(macro.Parameters[0]) < 0.001)
                                        clearOperatorUsed = true;

                                    macro.Parameters[1] /= scale;
                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    break;

                                case GerberApertureType.MacroOutline:
                                    if (Math.Abs(macro.Parameters[0]) < 0.001)
                                        clearOperatorUsed = true;

                                    for (int i = 2; i < numberOfParameters - 1; i++)
                                        macro.Parameters[i] /= scale;

                                    break;

                                case GerberApertureType.MacroPolygon:
                                    if (Math.Abs(macro.Parameters[0]) < 0.001)
                                        clearOperatorUsed = true;

                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    macro.Parameters[4] /= scale;
                                    break;

                                case GerberApertureType.MacroMoire:
                                    macro.Parameters[0] /= scale;
                                    macro.Parameters[1] /= scale;
                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    macro.Parameters[4] /= scale;
                                    macro.Parameters[6] /= scale;
                                    macro.Parameters[7] /= scale;
                                    break;

                                case GerberApertureType.MacroThermal:
                                    macro.Parameters[0] /= scale;
                                    macro.Parameters[1] /= scale;
                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    macro.Parameters[4] /= scale;
                                    break;

                                case GerberApertureType.MacroLine20:
                                    if (Math.Abs(macro.Parameters[0]) < 0.001)
                                        clearOperatorUsed = true;

                                    macro.Parameters[1] /= scale;
                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    macro.Parameters[4] /= scale;
                                    macro.Parameters[5] /= scale;
                                    break;

                                case GerberApertureType.MacroLine21:
                                case GerberApertureType.MacroLine22:
                                    if (Math.Abs(macro.Parameters[0]) < 0.001)
                                        clearOperatorUsed = true;

                                    macro.Parameters[1] /= scale;
                                    macro.Parameters[2] /= scale;
                                    macro.Parameters[3] /= scale;
                                    macro.Parameters[4] /= scale;
                                    break;

                                default:
                                    break;
                            }

                            aperture.SimplifiedMacroList.Add(macro);
                            //for (int i = 0; i < numberOfParameters; i++)
                            //    Debug.Write(String.Format("{0}, ", MacroStack.Values[i]));

                            //Debug.WriteLine(")");
                        }

                        MacroStack.Clear();
                        break;

                    default:
                        break;
                }
            }

            // Store a flag to let the renderer know if it should expect any "clear" primatives.
            //aperture.Parameters()[0] = clearOperatorUsed ? 1.0f : 0.0f;

            return success;
        }

        // Stack declarations and operations to be used by the simplify engine that executes the parsed aperture macros.
        private static class MacroStack
        {
            internal static double[] Values { get; set; }
            internal static int Count { get; set; }

            internal static void InitializeStack(int stackSize)
            {
                Values = new double[stackSize];
                Count = 0;
            }

            // Pushes a value onto the stack.
            internal static void Push(double value)
            {
                if (Count == Values.Length)
                    throw new MacroStackOverflowException("Attempt to push a full stack.");

                Values[Count++] = value;
                return;
            }

            // Pops a value off the stack.
            internal static double Pop()
            {
                if (Count == 0)
                    throw new MacroStackOverflowException("Attempt to pop an empty stack.");

                return Values[--Count];
            }

            // Reset the stack.
            internal static void Clear()
            {
                for (int i = 0; i < Values.Length; i++)
                    Values[i] = 0.0;

                Count = 0;
            }
        }
    }

    public class GerberInstruction
    {
        public GerberOpCode Opcode { get; set; }
        public Union Data;

        public GerberInstruction()
        {
            Data = new Union();
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct Union
        {
            // Set the offsets to the same position so that both variables occupy
            // the same memory address which is essentially C++ union does.
            [FieldOffset(0)]
            public double DoubleValue;
            [FieldOffset(0)]
            public int IntValue;
        }

    }
}

