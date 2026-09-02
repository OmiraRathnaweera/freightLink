from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    """Base for every schema exchanged with the C# backend.

    Field names are declared snake_case (Pythonic) but serialize/parse as
    camelCase, matching System.Text.Json's default casing on the backend's
    DTOs — zero manual translation layer between the two services.
    """

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)
