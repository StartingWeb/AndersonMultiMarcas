#!/usr/bin/env python3
"""Read-only brand UI regression on a loopback test host with known synthetic data.

Uses the same Python Playwright environment as browser_smoke.py. Supply
ANDERSON_E2E_BASE_URL, ANDERSON_E2E_EMAIL, ANDERSON_E2E_PASSWORD and
ANDERSON_E2E_BRAND_COUNTS (JSON mapping synthetic brand names to expected counts).
No brand/vehicle form is submitted; confirmation dialogs are always cancelled.
"""
import asyncio
import json
import os
import re
from pathlib import Path
from urllib.parse import urlparse

from playwright.async_api import async_playwright


async def main():
    base = os.environ.get('ANDERSON_E2E_BASE_URL', 'http://localhost:5185').rstrip('/')
    if urlparse(base).hostname not in ('localhost', '127.0.0.1', '::1'):
        raise SystemExit('Only a loopback test application is allowed.')
    expected = json.loads(os.environ['ANDERSON_E2E_BRAND_COUNTS'])
    assert expected and 0 in expected.values(), 'Include a synthetic brand without vehicles.'
    output = Path(os.environ.get('ANDERSON_E2E_REPORT_DIR', '.cache/marcas-validation/browser'))
    output.mkdir(parents=True, exist_ok=True)
    checkpoints, errors = [], []
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await browser.new_page(viewport={'width': 1440, 'height': 1000})
        page.on('pageerror', lambda error: errors.append(str(error)))
        await page.goto(base + '/Admin/Cadastros/Marcas')
        assert '/Login' in page.url
        await page.locator('#Input_Email').fill(os.environ['ANDERSON_E2E_EMAIL'])
        await page.locator('#passwordInput').fill(os.environ['ANDERSON_E2E_PASSWORD'])
        await page.locator('button[type=submit]').click()
        await page.wait_for_url('**/Admin/**')

        for width in (1440, 768, 390):
            await page.set_viewport_size({'width': width, 'height': 1000 if width == 1440 else 844})
            response = await page.goto(base + '/Admin/Cadastros/Marcas')
            assert response.status == 200
            assert await page.locator('h1').inner_text() == 'Marcas'
            assert await page.locator('h1').count() == 1
            assert await page.locator('.admin-title strong').count() == 0
            assert await page.locator('.admin-title').inner_text() == 'Anderson Multimarcas'
            assert await page.locator('.page-subtitle').inner_text() == 'Lista e gerenciamento das marcas cadastradas'
            assert await page.get_by_role('link', name=re.compile(r'Nova marca$')).is_visible()
            assert 'Veículos' in await page.locator('thead').text_content()
            assert 'Ações' in await page.locator('thead').text_content()
            assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1')
            for name, count in expected.items():
                row = page.locator('tbody tr').filter(has=page.get_by_text(name, exact=True))
                assert await row.count() == 1
                cell = row.locator('.vehicle-count')
                assert await cell.inner_text() == str(count)
                assert await cell.evaluate("e => getComputedStyle(e).textAlign") == 'right'
                bounds = await cell.bounding_box()
                assert bounds['x'] + bounds['width'] <= width + 1, 'Counts must be visible without horizontal scrolling.'
                assert await row.get_by_role('link', name='Editar marca', exact=True).count() == 1
            actions = page.locator('tbody tr').first.get_by_role('button', name='Excluir marca', exact=True)
            await actions.scroll_into_view_if_needed()
            assert (await actions.bounding_box())['width'] >= 38
            await actions.click()
            assert await page.get_by_role('dialog').is_visible()
            assert await page.get_by_role('heading', name='Confirmar exclusão', exact=True).is_visible()
            await page.get_by_role('button', name='Cancelar', exact=True).click()
            assert not await page.get_by_role('dialog').is_visible()
            # Reset horizontal scrolling so the capture starts at the brand column.
            await page.locator('.table-responsive').evaluate('e => e.scrollLeft = 0')
            await page.screenshot(path=str(output / f'marcas-{width}.png'), full_page=True)
            if width == 390:
                await page.get_by_role('button', name='Abrir menu', exact=True).click()
                assert await page.locator('body').evaluate("e => e.classList.contains('admin-menu-open')")
                await page.locator('[data-admin-close]').click(position={'x': width - 5, 'y': 5})
                assert not await page.locator('body').evaluate("e => e.classList.contains('admin-menu-open')")
            checkpoints.append(f'listing, counts, title, accents, actions and overflow at {width}px')

        await page.get_by_role('textbox', name='Pesquisar marca', exact=True).fill(next(iter(expected)))
        await page.get_by_role('button', name=re.compile(r'Buscar$')).click()
        assert await page.locator('tbody tr').count() == 1
        await page.get_by_role('link', name='Editar marca', exact=True).click()
        assert await page.locator('h1').inner_text() == 'Editar marca'
        assert await page.get_by_role('button', name=re.compile(r'Salvar alterações$')).is_visible()
        assert await page.get_by_text('Preencha as informações principais para exibição e filtros.', exact=True).is_visible()
        await page.screenshot(path=str(output / 'marcas-editar-mobile.png'), full_page=True)
        await page.get_by_role('link', name=re.compile(r'Voltar$')).click()
        await page.get_by_role('link', name=re.compile(r'Nova marca$')).click()
        assert await page.get_by_role('button', name=re.compile(r'Cadastrar marca$')).is_visible()
        assert await page.locator('#Marca_Nome').get_attribute('data-val-length') == 'O nome deve ter no máximo 100 caracteres.'
        assert await page.locator('#Marca_LogoUrl').get_attribute('data-val-length') == 'A URL deve ter no máximo 400 caracteres.'
        checkpoints.append('search and create/edit navigation with accented help and validation')

        await page.goto(base + '/Admin/Cadastros/Marcas?Filtro=__synthetic_no_matches__')
        assert await page.get_by_role('heading', name='Nenhuma marca encontrada', exact=True).is_visible()
        assert await page.get_by_text('Ajuste os filtros e tente novamente.', exact=True).is_visible()
        checkpoints.append('empty listing')
        assert not errors, errors
        await browser.close()
    (output / 'results.json').write_text(json.dumps({'passed': True, 'checkpoints': checkpoints,
        'javascriptErrors': errors}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Brand browser regression passed: {len(checkpoints)} scenarios, no JavaScript errors.')


if __name__ == '__main__':
    asyncio.run(main())
