/** The dashboard's stat cards and trainings-per-month chart (SPEC section 6.2). */
export interface ClubNumbersInfo {
  totalMembers: number;
  activeMembers: number;
  trainingsHeld: number;
  attendancePercentage: number;
  trainingsPerMonth: MonthlyTrainingCount[];
}

export interface MonthlyTrainingCount {
  year: number;
  month: number;
  count: number;
}
