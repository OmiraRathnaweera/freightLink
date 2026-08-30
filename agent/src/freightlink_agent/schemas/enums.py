from typing import Literal

# Mirrors backend/Entities/Enums/VehicleClass.cs exactly (serialized as a string
# via JsonStringEnumConverter on the C# side).
VehicleClass = Literal["MiniTruck", "MediumLorry", "ContainerTruck"]

# Mirrors backend/Entities/Enums/AgentRole.cs.
AgentRole = Literal["Planner", "DomainAnalysis", "MatchingPricing", "ValidationSafety"]

# Mirrors backend/Entities/Enums/AgentStepStatus.cs.
AgentStepStatus = Literal["Pending", "Running", "Succeeded", "Failed"]

# Mirrors backend/Entities/Enums/ToolName.cs.
ToolName = Literal["get_route_and_eta", "estimate_price"]
