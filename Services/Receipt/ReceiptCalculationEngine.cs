using resturanyar.Models.Receipt;

namespace resturanyar.Services.Receipt
{
    public class ChargeCalculationInput
    {
        public int? DefinitionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ChargeCategory Category { get; set; }
        public ChargeCalculationType CalculationType { get; set; }
        public decimal Value { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTaxable { get; set; }
        public PercentageBaseKind PercentageBase { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ReceiptCalculationResult
    {
        public decimal ItemsSubtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal FeesTotal { get; set; }
        public decimal TaxTotal { get; set; }
        public decimal GrandTotal { get; set; }
        public List<ReceiptChargeLineDto> ChargeLines { get; set; } = new();
    }

    public interface IReceiptCalculationEngine
    {
        ReceiptCalculationResult Calculate(decimal itemsSubtotal, List<ChargeCalculationInput> charges);
    }

    public class ReceiptCalculationEngine : IReceiptCalculationEngine
    {
        public ReceiptCalculationResult Calculate(decimal itemsSubtotal, List<ChargeCalculationInput> charges)
        {
            var result = new ReceiptCalculationResult
            {
                ItemsSubtotal = Round(itemsSubtotal)
            };

            var active = charges
                .Where(c => c.IsEnabled)
                .ToList();

            var discounts = active
                .Where(c => c.Category == ChargeCategory.Discount)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            var fees = active
                .Where(c => c.Category == ChargeCategory.Fee)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            var taxes = active
                .Where(c => c.Category == ChargeCategory.Tax)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            decimal discountTotal = 0;
            decimal taxableDiscountTotal = 0;

            foreach (var discount in discounts)
            {
                var amount = CalculateAmount(discount, result.ItemsSubtotal, 0);
                var remaining = result.ItemsSubtotal - discountTotal;
                if (amount > remaining)
                    amount = remaining < 0 ? 0 : remaining;

                discountTotal += amount;
                if (discount.IsTaxable)
                    taxableDiscountTotal += amount;

                result.ChargeLines.Add(ToLine(discount, amount));
            }

            decimal feesTotal = 0;
            decimal taxableFeesTotal = 0;
            decimal priorFees = 0;

            foreach (var fee in fees)
            {
                var baseAmount = fee.PercentageBase == PercentageBaseKind.ItemsNetPlusPriorFees
                    ? result.ItemsSubtotal + priorFees
                    : result.ItemsSubtotal;

                var amount = CalculateAmount(fee, baseAmount, priorFees);
                feesTotal += amount;
                priorFees += amount;
                if (fee.IsTaxable)
                    taxableFeesTotal += amount;

                result.ChargeLines.Add(ToLine(fee, amount));
            }

            var taxableBase = Round(result.ItemsSubtotal - taxableDiscountTotal + taxableFeesTotal);
            if (taxableBase < 0)
                taxableBase = 0;

            decimal taxTotal = 0;

            foreach (var tax in taxes)
            {
                var amount = tax.CalculationType == ChargeCalculationType.Percentage
                    ? Round(taxableBase * tax.Value / 100m)
                    : Round(tax.Value);

                taxTotal += amount;
                result.ChargeLines.Add(ToLine(tax, amount));
            }

            result.DiscountTotal = Round(discountTotal);
            result.FeesTotal = Round(feesTotal);
            result.TaxTotal = Round(taxTotal);
            var grand = Round(result.ItemsSubtotal - result.DiscountTotal + result.FeesTotal + result.TaxTotal);
            result.GrandTotal = grand < 0 ? 0 : grand;

            return result;
        }

        private static decimal CalculateAmount(ChargeCalculationInput charge, decimal baseAmount, decimal priorFees)
        {
            if (charge.CalculationType == ChargeCalculationType.Fixed)
                return Round(charge.Value);

            var percentageBase = charge.PercentageBase == PercentageBaseKind.ItemsNetPlusPriorFees
                ? baseAmount
                : baseAmount;

            return Round(percentageBase * charge.Value / 100m);
        }

        private static ReceiptChargeLineDto ToLine(ChargeCalculationInput charge, decimal amount) => new()
        {
            DefinitionId = charge.DefinitionId,
            Code = charge.Code,
            Title = charge.Title,
            Category = charge.Category,
            CalculationType = charge.CalculationType,
            Value = charge.Value,
            CalculatedAmount = amount,
            IsTaxable = charge.IsTaxable,
            DisplayOrder = charge.DisplayOrder
        };

        private static decimal Round(decimal value)
            => Math.Round(value, 0, MidpointRounding.AwayFromZero);
    }
}
