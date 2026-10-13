$(document).ready(function () {
    
    // Inicializar DataTables en todas las tablas que NO tengan la clase 'no-datatable'
    // Esto nos da control total desde el HTML
    $('table:not(.no-datatable)').DataTable({
        language: {
            url: '//cdn.datatables.net/plug-ins/1.13.4/i18n/es-ES.json'
        },
        responsive: true,
        dom: 'Bfrtip',
        buttons: [
            {
                extend: 'excelHtml5',
                text: '<i class="fas fa-file-excel"></i> Excel',
                className: 'btn btn-success btn-sm',
                titleAttr: 'Exportar a Excel'
            },
            {
                extend: 'pdfHtml5',
                text: '<i class="fas fa-file-pdf"></i> PDF',
                className: 'btn btn-danger btn-sm',
                titleAttr: 'Exportar a PDF'
            },
            {
                extend: 'print',
                text: '<i class="fas fa-print"></i> Imprimir',
                className: 'btn btn-secondary btn-sm',
                titleAttr: 'Imprimir Tabla'
            }
        ],
        order: [[0, 'desc']] 
    });

    // Inicializar Select2 en cualquier select con la clase .select2
    $('.select2').select2({
        theme: 'bootstrap-5',
        placeholder: "Seleccione una opción...",
        allowClear: true,
        width: '100%'
    });
});