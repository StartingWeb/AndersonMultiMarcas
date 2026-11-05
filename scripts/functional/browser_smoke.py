#!/usr/bin/env python3
"""Playwright regression against an explicitly configured loopback app and synthetic SQL data."""
import asyncio
import json
import os
from pathlib import Path
import re
import time
from urllib.parse import urlparse
from playwright.async_api import async_playwright

BASE = os.environ.get('ANDERSON_E2E_BASE_URL', 'http://127.0.0.1:5185').rstrip('/')
REPORT = Path(os.environ.get('ANDERSON_E2E_REPORT', '.cache/validation/browser-results.json'))
CHECKPOINTS, JAVASCRIPT_ERRORS = [], []


async def main():
    if urlparse(BASE).hostname not in ('localhost', '127.0.0.1', '::1'):
        raise SystemExit('Only a loopback test application is allowed.')
    password = os.environ['ANDERSON_E2E_PASSWORD']
    email = os.environ.get('ANDERSON_E2E_EMAIL', 'developer@example.invalid')
    checkpoints, errors = CHECKPOINTS, JAVASCRIPT_ERRORS
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps({'passed': False, 'status': 'running', 'checkpoints': []}) + '\n')
    stamp = str(time.time_ns())
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await browser.new_page(viewport={'width': 1440, 'height': 1000})
        page.on('pageerror', lambda error: errors.append(str(error)))
        async def visit(path, expected=200):
            response = await page.goto(BASE + path, wait_until='domcontentloaded')
            assert response.status == expected, (path, response.status)
            assert '\ufffd' not in await page.locator('body').inner_text(), ('invalid text encoding', path)
            checkpoints.append({'page': path, 'http': response.status})
        async def save():
            await page.locator('button[type=submit]').last.click()
            await page.wait_for_load_state('domcontentloaded')
        for path in ['/', '/veiculos', '/veiculos?OrdenarPor=preco-asc', '/veiculos?OrdenarPor=preco-desc',
                     '/veiculos?PrecoMinimo=35000&PrecoMaximo=50000', '/veiculos?page=2',
                     '/veiculos/zero-km', '/veiculos/seminovos', '/veiculo/1/', '/Empresa', '/Contato', '/Privacy']:
            await visit(path)
        await visit('/veiculo/41/', 404)
        await visit('/veiculo/42/', 404)
        await visit('/veiculo/1/')
        for text in await page.locator('script[type="application/ld+json"]').all_text_contents():
            schema = json.loads(text)
            assert '@context' in schema or isinstance(schema, list)
        assert await page.locator('a[href*="wa.me"], [data-whatsapp]').count() > 0
        await page.screenshot(path=str(REPORT.parent / 'public-detail-desktop.png'), full_page=True)
        for width in [390, 768]:
            await page.set_viewport_size({'width': width, 'height': 844})
            for path in ['/', '/veiculos', '/veiculo/1/', '/Contato']:
                await visit(path)
                assert await page.evaluate('document.documentElement.scrollWidth <= innerWidth + 1'), ('horizontal overflow', path, width)
            await page.screenshot(path=str(REPORT.parent / f'public-contact-{width}.png'), full_page=True)
        await page.set_viewport_size({'width': 1440, 'height': 1000})
        r = await page.goto(BASE + '/Admin/Veiculo')
        assert '/Login' in page.url
        checkpoints.append({'flow': 'anonymous admin redirected to login', 'http': r.status})
        response = await page.request.post(BASE + '/api/veiculos', data={})
        assert response.status == 401
        await visit('/Login')
        await page.locator('#Input_Email').fill(email)
        await page.locator('#passwordInput').fill(password)
        await save()
        await page.wait_for_url(re.compile(r'/Admin/'))
        checkpoints.append({'flow': 'real SQL Identity login', 'passed': True})
        for path in ['/Admin', '/Admin/Veiculo', '/Admin/VeiculosVenda', '/Admin/Cadastros/Lojas',
                     '/Admin/Cadastros/Marcas', '/Admin/Cadastros/Vendedores', '/Admin/Auth/Usuarios',
                     '/Admin/Auth/Perfil', '/Admin/Storage/ImportarImagens', '/Admin/Storage/ValidarImportacao',
                     '/Admin/Ferramentas/SincronizarImagensR2', '/Admin/Ferramentas/RevisarCapasVeiculos']:
            await visit(path)
        await visit('/Admin/Cadastros/Lojas/Upsert')
        fields = {'Nome': 'Loja navegador ' + stamp, 'RazaoSocial': 'Dados sinteticos', 'Email': 'browser@example.invalid',
                  'Telefone': '16999990000', 'Logradouro': 'Rua Teste', 'Numero': '1', 'Bairro': 'Centro',
                  'Cidade': 'Taquaritinga', 'Cep': '15900000'}
        for key, value in fields.items(): await page.locator('#Loja_' + key).fill(value)
        await page.locator('#Loja_Uf').select_option('SP')
        await save()
        await page.wait_for_url(re.compile(r'/Admin/Cadastros/Lojas$'))
        assert await page.get_by_text(fields['Nome'], exact=True).count() == 1
        row = page.locator('tr').filter(has_text=fields['Nome'])
        href = await row.locator('a[href*="Upsert"]').get_attribute('href')
        await visit(href)
        assert await page.locator('#Loja_Cnpj').input_value() == ''
        await page.locator('#Loja_RazaoSocial').fill('Edicao sintetica')
        await save()
        await page.wait_for_url(re.compile(r'/Admin/Cadastros/Lojas$'))
        await visit(href)
        assert await page.locator('#Loja_RazaoSocial').input_value() == 'Edicao sintetica'
        assert await page.locator('#Loja_Cnpj').input_value() == ''
        checkpoints.append({'flow': 'store create/edit persists with optional CNPJ', 'passed': True})
        await visit('/Admin/Cadastros/Marcas/Upsert')
        brand = 'Marca navegador ' + stamp
        await page.locator('#Marca_Nome').fill(brand)
        await save()
        await page.wait_for_url(re.compile(r'/Admin/Cadastros/Marcas$'))
        assert await page.get_by_text(brand, exact=True).count() == 1
        checkpoints.append({'flow': 'brand create persists', 'passed': True})
        await visit('/Admin/Cadastros/Vendedores/Upsert')
        seller = 'Vendedor navegador ' + stamp
        await page.locator('#Vendedor_Nome').fill(seller)
        await page.locator('#Vendedor_LojaId').select_option(index=1)
        await page.locator('#Vendedor_Email').fill('seller@example.invalid')
        await page.locator('#Vendedor_Telefone').fill('16999990000')
        await page.locator('#Vendedor_Whatsapp').fill('16999990000')
        await save()
        await page.wait_for_url(re.compile(r'/Admin/Cadastros/Vendedores$'))
        assert await page.get_by_text(seller, exact=True).count() == 1
        checkpoints.append({'flow': 'seller create persists', 'passed': True})
        await visit('/Admin/Veiculo/Upsert')
        await page.locator('#Veiculo_LojaId').select_option(index=1)
        await page.locator('#Veiculo_MarcaId').select_option(index=1)
        for key, value in {'Modelo': 'Browser ' + stamp, 'Versao': 'Teste', 'AnoFabricacao': '2025', 'AnoModelo': '2026',
                           'Cor': 'Branco', 'Quilometragem': '0', 'PrecoVenda': '99.000,00'}.items():
            await page.locator('#Veiculo_' + key).fill(value)
        await page.locator('#Veiculo_Combustivel').select_option('3')
        await page.locator('#Veiculo_Cambio').select_option('2')
        await page.locator('#Veiculo_Ativo').check()
        await page.locator('#Veiculo_Seminovo').uncheck()
        await save()
        await page.wait_for_url(re.compile(r'/Admin/Veiculo/Upsert/\d+'))
        vehicle_url = page.url
        assert await page.locator('#NovasMidias').count() == 1
        await page.locator('#NovasMidias').set_input_files('Project/wwwroot/img/carroDefault.png')
        await save()
        await page.wait_for_load_state('domcontentloaded')
        assert not await page.locator('.validation-summary-errors li:visible').count()
        await visit(urlparse(page.url).path + '?step=midias')
        stored_image = page.locator('#mediaList [data-kind="existing"] img').first
        assert await stored_image.count() == 1
        uploaded_url = await stored_image.get_attribute('src')
        assert uploaded_url and uploaded_url.startswith('/uploads/veiculos/')
        uploaded_response = await page.request.get(BASE + uploaded_url)
        assert uploaded_response.status == 200
        assert uploaded_response.headers.get('content-type', '').startswith('image/')
        assert len(await uploaded_response.body()) > 0
        uploaded_basename = urlparse(uploaded_url).path.rsplit('/', 1)[1]
        checkpoints.append({'flow': 'uploaded image persists after reload and returns image bytes', 'passed': True})
        checkpoints.append({'flow': 'vehicle create zero km and image upload', 'passed': True, 'route': urlparse(vehicle_url).path})
        vehicle_id = re.search(r'/Upsert/(\d+)', vehicle_url).group(1)
        await visit('/veiculo/' + vehicle_id + '/')
        assert '99.000,00' in await page.locator('body').inner_text()
        await page.locator('img[src*="' + uploaded_basename + '"]').first.scroll_into_view_if_needed()
        await page.wait_for_function('(name) => Array.from(document.images).some(i => i.src.includes(name) && i.complete && i.naturalWidth > 0)', arg=uploaded_basename)
        await page.wait_for_function("Array.from(document.images).filter(i=>i.getAttribute('src') && i.getClientRects().length && i.loading!=='lazy').every(i=>i.complete && i.naturalWidth>0)")
        checkpoints.append({'flow': 'uploaded image renders on public vehicle with saved price', 'passed': True})
        await visit('/Admin/Ferramentas/RevisarCapasVeiculos?Mode=Todos&Busca=' + vehicle_id)
        assert await page.locator('input[name=mediaId]').count() > 0
        await page.locator('input[name=mediaId]').first.check()
        async with page.expect_response(lambda r: 'handler=SaveCover' in r.url) as response_info:
            await page.locator('#save-cover').click()
        cover_response = await response_info.value
        assert cover_response.status == 200
        assert (await cover_response.json())['success']
        checkpoints.append({'flow': 'cover review protected AJAX save succeeds', 'passed': True})
        await visit('/Admin/Veiculo')
        assert await page.get_by_text('Browser ' + stamp, exact=False).count() > 0
        await page.screenshot(path=str(REPORT.parent / 'admin-vehicles-desktop.png'), full_page=True)
        # Logout is a protected POST from the administration layout.
        logout = page.locator('form[action*="Logout"]')
        assert await logout.count() > 0
        await logout.locator('button').click()
        await page.wait_for_url(re.compile(r'/Login'))
        await page.goto(BASE + '/Admin/Veiculo')
        assert '/Login' in page.url
        checkpoints.append({'flow': 'logout removes authenticated session', 'passed': True})
        assert not errors, errors
        await browser.close()
    REPORT.write_text(json.dumps({'passed': True, 'checkpoints': checkpoints, 'javascriptErrors': errors}, indent=2) + '\n')
    print(f'Browser validation passed: {len(checkpoints)} checkpoints; no JavaScript errors.')


if __name__ == '__main__':
    try:
        asyncio.run(main())
    except BaseException as error:
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text(json.dumps({'passed': False, 'errorType': type(error).__name__,
            'checkpoints': CHECKPOINTS, 'javascriptErrors': JAVASCRIPT_ERRORS}, indent=2) + '\n')
        raise
