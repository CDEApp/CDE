﻿using cdeLibSpec2.TestStack.BDDfy.Samples.BuyingTrainFares;
using NUnit.Framework;
using TestStack.BDDfy;

namespace cdeLibSpec2.TestStack.BDDfy.Samples
{
    public class BuyingTrainFareWithExamples
    {
        #pragma warning disable 649
        // ReSharper disable once InconsistentNaming
        private Fare fare;
        private BuyerCategory _buyerCategory;
        #pragma warning restore 649
        Money Price { get; set; }

        [Test]
        public void SuccessfulRailCardPurchases()
        {
            this.Given(_ => TheBuyerIsA(_buyerCategory))
                .And(_ => TheBuyerSelectsA(fare))
                .When(_ => TheBuyerPays())
                .Then(_ => ASaleOccursWithAnAmountOf(Price))
                .WithExamples(new ExampleTable(
                    "Buyer Category", "Fare", "Price")
                {
                    {BuyerCategory.Student, new MonthlyPass(), new Money(76)},
                    {BuyerCategory.Senior, new MonthlyPass(), new Money(98)},
                    {BuyerCategory.Standard, new MonthlyPass(), new Money(146)},
                    {BuyerCategory.Student, new WeeklyPass(), new Money(23)},
                    {BuyerCategory.Senior, new WeeklyPass(), new Money(30)},
                    {BuyerCategory.Standard, new WeeklyPass(), new Money(44)},
                    {BuyerCategory.Student, new DayPass(), new Money(4)},
                    {BuyerCategory.Senior, new DayPass(), new Money(5)},
                    {BuyerCategory.Standard, new DayPass(), new Money(7)},
                    {BuyerCategory.Student, new SingleTicket(), new Money(1.5m)},
                    {BuyerCategory.Senior, new SingleTicket(), new Money(2m)},
                    {BuyerCategory.Standard, new SingleTicket(), new Money(3m)}
                })
                .BDDfy("Successful rail card purchases");
        }

        void TheBuyerIsA(BuyerCategory buyerCategory)
        {

        }

        void TheBuyerSelectsA(Fare fare)
        {

        }

        void TheBuyerPays()
        {

        }

        void ASaleOccursWithAnAmountOf(Money price)
        {

        }
    }
}