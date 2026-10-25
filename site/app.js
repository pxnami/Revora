const previews = {
  overview: { description: 'Your device, its mode, and the next step. In one place.', alt: 'Revora overview showing a synthetic test iPad, its connection state, and recovery controls' },
  recovery: { description: 'Recovery controls that follow your device’s current mode.', alt: 'Revora recovery screen with mode-aware controls for a synthetic test device' },
  firmware: { description: 'A local IPSW workflow with device compatibility checks.', alt: 'Revora firmware screen showing local IPSW selection and restore options for a synthetic test device' }
};

const preview = document.querySelector('#app-preview');
const description = document.querySelector('#preview-description');
const controls = document.querySelectorAll('[data-view]');

controls.forEach((button) => {
  button.addEventListener('click', () => {
    const view = button.dataset.view;
    controls.forEach((control) => control.setAttribute('aria-pressed', String(control === button)));
    preview.src = `assets/${view}.png`;
    preview.alt = previews[view].alt;
    description.textContent = previews[view].description;
  });
});
