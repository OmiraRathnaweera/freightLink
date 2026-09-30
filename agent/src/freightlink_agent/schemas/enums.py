from typing import Literal

# Mirrors backend/Entities/Enums/AgentRole.cs. Only "Planner" is emitted by
# this service today - the other three values exist because they're real
# members of the backend enum, not because those agents are implemented here.
AgentRole = Literal["Planner", "DomainAnalysis", "MatchingPricing", "ValidationSafety"]

# Mirrors backend/Entities/Enums/AgentStepStatus.cs.
AgentStepStatus = Literal["Pending", "Running", "Succeeded", "Failed"]

# Mirrors backend/Entities/Enums/VehicleClass.cs.
VehicleClass = Literal["MiniTruck", "MediumLorry", "ContainerTruck"]

# Mirrors backend/Entities/Enums/ToolName.cs.
ToolName = Literal["get_route_and_eta", "estimate_price"]
