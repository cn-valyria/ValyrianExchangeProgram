export const transactionClassifications = [
  { id: 0, description: "Non-VEP aid" },
  { id: 1, description: "VEP aid" },
  { id: 2, description: "Credit to B or W" },
  { id: 3, description: "Credit to S or N or Q" },
  { id: 4, description: "Credit for wonder" },
  { id: 5, description: "Debt to B or P" },
  { id: 6, description: "Debt to S or N or V" }
];

export const accountRoles = [
  { code: "B", description: "Buyer" },
  { code: "S", description: "Seller" },
  { code: "D", description: "Cash Donor" },
  { code: "F", description: "Tech Farm" },
  { code: "C", description: "Cash Collector" },
  { code: "R", description: "Tech Receiver" },
  { code: "N", description: "Probationary Seller" },
  { code: "P", description: "Temporary Donor" },
  { code: "V", description: "Temporary Collector" },
  { code: "Q", description: "Temporary Farm" },
  { code: "W", description: "Temporary Receiver" },
  { code: "H", description: "On Hold" },
];

export const recentActivityValues = [
  { id: 1, description: "Last 3 Days" },
  { id: 2, description: "This Week" },
  { id: 3, description: "Last Week" },
  { id: 4, description: "Three Weeks Ago" },
  { id: 5, description: "More Than 3 Weeks Ago" }
];