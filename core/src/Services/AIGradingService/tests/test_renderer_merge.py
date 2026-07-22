"""Tests for document merge helper used when ingesting rubric + exam paper."""

from __future__ import annotations

from app.services.normalize.renderer import EmbeddedImage, RenderedDocument, RenderedPage, merge_rendered_documents


def test_merge_rendered_documents_appends_pages():
    primary = RenderedDocument(
        sha256="a",
        pages=[RenderedPage(page_index=0, width=10, height=10, image_png=b"p0", text="rubric")],
        embedded_images=[],
    )
    extra = RenderedDocument(
        sha256="b",
        pages=[RenderedPage(page_index=0, width=10, height=10, image_png=b"p1", text="exam")],
        embedded_images=[EmbeddedImage(page_index=0, image_index=0, data=b"img", ext="png")],
    )
    merged = merge_rendered_documents(primary, extra)
    assert len(merged.pages) == 2
    assert merged.pages[1].page_index == 1
    assert merged.pages[1].text == "exam"
    assert len(merged.embedded_images) == 1
