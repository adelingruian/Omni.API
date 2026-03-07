namespace Omni.Web.Services
{
    public static class DisruptionScorecard
    {
        public static (double TotalPoints, string Severity) Calculate(
            int delayMins,
            int passengers,
            bool isGateOccupied,
            bool isRunwayConflict,
            int totalBagsOnBelt)
        {
            double points = 0;

            // 1. Delay Points: (Minutes/10)^2 * (Passengers/10)
            if (delayMins > 0)
            {
                points += Math.Pow(delayMins / 10.0, 2) * (passengers / 10.0);
            }

            // 2. Legal Fines: +25 points per passenger if delay >= 3 hours
            if (delayMins >= 180)
            {
                points += (passengers * 25);
            }

            // 3. Parking Problems: +500 points if gate is occupied
            if (isGateOccupied)
            {
                points += 500;
            }

            // 4. Safety Risks: Massive 4000+ point boost for runway conflicts
            if (isRunwayConflict)
            {
                points += 4000;
            }

            // 5. Luggage Jams: Points for every bag over the 200 limit
            if (totalBagsOnBelt > 200)
            {
                double extraBags = totalBagsOnBelt - 200;
                points += Math.Pow(extraBags / 10.0, 2) * 5;
            }

            points = Math.Round(points, 0);
            string severity = GetTier(points);

            return (points, severity);
        }

        private static string GetTier(double score)
        {
            if (score <= 0) return "On Time";
            if (score < 100) return "Low Risk";
            if (score < 500) return "Medium Risk";
            if (score < 2000) return "High Risk";
            return "CRITICAL";
        }
    }
}
