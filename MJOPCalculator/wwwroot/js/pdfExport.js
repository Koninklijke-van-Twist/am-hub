window.exportHtmlToPrint = async (html) => {
    const element = document.createElement('div');
    element.innerHTML = html;

    const options = {
        filename: 'offerte.pdf',
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: { scale: 2 },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
    };

    const worker = html2pdf().set(options).from(element);
    const pdfBlob = await worker.outputPdf('blob');
    const blobUrl = URL.createObjectURL(pdfBlob);

    window.open(blobUrl, '_blank');
};


window.exportHtmlToDownload = (html, fileName) => {
    const element = document.createElement('div');
    element.innerHTML = html;

    const options = {
        filename: fileName || 'offerte.pdf',
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: { scale: 2 },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
    };

    html2pdf().set(options).from(element).save();
};
