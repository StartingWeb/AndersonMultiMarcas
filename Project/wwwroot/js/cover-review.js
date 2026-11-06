(function () {
    const form = document.getElementById('cover-form');
    if (!form) return;
    const message = document.getElementById('cover-message');
    const button = document.getElementById('save-cover');
    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        const selected = form.querySelector('input[name="mediaId"]:checked');
        if (!selected || !button || button.disabled) return;
        button.disabled = true;
        message.textContent = 'Salvando capa...';
        try {
            const response = await fetch(location.pathname + '?handler=SaveCover', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': form.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    vehicleId: Number(form.dataset.vehicleId),
                    mediaId: Number(selected.value),
                    position: Number(form.dataset.position),
                    mode: form.dataset.mode,
                    busca: form.dataset.search,
                    maisQue: Number(form.dataset.moreThan)
                })
            });
            if (!response.ok) throw new Error('Nao foi possivel salvar a capa. Atualize a pagina e tente novamente.');
            const result = await response.json();
            if (!result.success) throw new Error(result.message);
            message.textContent = result.message;
            if (result.nextPosition !== null) {
                const next = new URL(location.href);
                next.searchParams.set('Position', result.nextPosition);
                location.assign(next.href);
            }
        } catch (error) {
            message.textContent = error.message || 'Nao foi possivel salvar a capa.';
        } finally {
            button.disabled = false;
        }
    });
})();
