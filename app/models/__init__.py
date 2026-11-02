"""
Pydantic models with validation.
"""
from __future__ import annotations

from typing import Any, Optional

from fastapi import HTTPException
from pydantic import BaseModel, Field, field_validator


class FCRRecord(BaseModel):
    id:               Optional[int]   = None
    row_num:          str             = Field(default="", max_length=20)
    fcr_subject:      str             = Field(default="", min_length=1, max_length=500)
    category:         str             = Field(default="", max_length=100)
    fcr_code:         str             = Field(default="", max_length=50)
    date:             str             = Field(default="", max_length=20)
    review_date:      str             = Field(default="", max_length=20)
    originator:       str             = Field(default="", max_length=100)
    action_party:     str             = Field(default="", max_length=100)
    level:            str             = Field(default="", max_length=50)
    session_code:     str             = Field(default="", max_length=50)
    pedec_status:     str             = Field(default="", max_length=100)
    fcr_status:       str             = Field(default="", max_length=100)
    execution_status: str             = Field(default="", max_length=100)
    closed_date:      str             = Field(default="", max_length=20)
    tr:               str             = Field(default="", max_length=50)
    next_action_by:   str             = Field(default="", max_length=100)
    arch_loc:         str             = Field(default="", max_length=100)
    last_update:      str             = Field(default="", max_length=20)
    remarks:          str             = Field(default="", max_length=1000)

    @field_validator("fcr_subject")
    @classmethod
    def subject_not_empty(cls, v: str) -> str:
        if not v or not v.strip():
            raise HTTPException(400, "FCR Subject required")
        return v.strip()

    @field_validator("fcr_code")
    @classmethod
    def validate_code_format(cls, v: str) -> str:
        # Validation happens in service layer with validate_fcr_code
        return v.strip().upper() if v else ""


class BulkSaveRequest(BaseModel):
    records: list[FCRRecord]


class BulkDeleteRequest(BaseModel):
    ids: list[int]


class SettingsModel(BaseModel):
    company_name:             Optional[str] = None
    attachment_root:          Optional[str] = None
    backup_location:          Optional[str] = None
    dark_mode:                Optional[bool] = None
    language:                 Optional[str] = None
    dept_codes:               Optional[dict] = None
    auto_backup_enabled:      Optional[bool] = None
    auto_backup_interval_days: Optional[int] = None
    auto_export_enabled:      Optional[bool] = None
    auto_export_location:     Optional[str] = None
    auto_export_interval_days: Optional[int] = None
    pedec_alert_days:         Optional[int] = None