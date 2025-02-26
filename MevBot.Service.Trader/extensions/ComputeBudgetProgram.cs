using Solnet.Rpc.Models;
using Solnet.Wallet;

public static class ComputeBudgetProgram
{
    /// <summary>
    /// Creates a RequestUnits instruction for the Compute Budget Program.
    /// This instruction requests extra compute units and includes an additional fee tip.
    /// </summary>
    /// <param name="units">The number of compute units requested.</param>
    /// <param name="additionalFee">The additional fee (in lamports) to attach as a tip.</param>
    /// <returns>A TransactionInstruction for the Compute Budget Program.</returns>
    public static TransactionInstruction RequestUnits(uint units, ulong additionalFee)
    {
        // Instruction layout:
        //   1 byte: instruction type (0 for RequestUnits)
        //   4 bytes: number of compute units (uint32)
        //   8 bytes: additional fee (uint64)
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((byte)0); // RequestUnits instruction type
        bw.Write(units);
        bw.Write(additionalFee);
        var data = ms.ToArray();

        return new TransactionInstruction
        {
            ProgramId = new PublicKey("ComputeBudget111111111111111111111111111111"),
            Keys = new List<AccountMeta>(), // No additional accounts are needed
            Data = data
        };
    }
}