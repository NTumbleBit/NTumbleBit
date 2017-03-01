const apiUri = "http://" + window.location.hostname +":" + window.location.port + "/api"
const solverServerSessionStatesUri = "/SolverServerSessionStates";
const promiseServerSessionStatesUri = "/PromiseServerSessionStates";
const cycleStateUri = "/Cycles";
const blockHeightUri = "/BlockHeight";
const feeUri = "/Fee";
const denominationUri = "/Denomination";

const SATOSHI_TO_BTC = 100000000;

const solverServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingPuzzles": 2,
  "WaitingRevelation": 3,
  "WaitingBlindFactor": 4,
  "WaitingFulfillment": 5,
  "Completed": 6
};

const promiseServerSessionStates = {
  "WaitingEscrow": 1,
  "WaitingHashes": 2,
  "WaitingRevelation": 3,
  "Completed": 4
};

const vm = new Vue();

let cycles = [];

(function poll() {
  fetchEndpoints();
  setTimeout(poll, 10000);
})();

function fetchEndpoints() {
  fetchBlockHeight();

  fetch(apiUri + cycleStateUri)
    .then(handleErrors)
    .then(res => res.json())
    .then(cycles => updateCycles(cycles));
}

(function fetchDenomination() {
  fetch(apiUri + denominationUri)
  .then(handleErrors)
    .then((res) => res.json())
    .then((denom) => updateDenomination(denom));
})();

(function fetchFee() {
  fetch(apiUri + feeUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((fee) => updateFee(fee));
})();

function fetchBlockHeight() {
  fetch(apiUri + blockHeightUri)
    .then(handleErrors)
    .then((res) => res.json())
    .then((height) => updateBlockHeight(height));
}

function handleErrors(res) {
  if (!res.ok) {
    throw Error(res.statusText);
  }
  return res;
}

function updateDenomination(denom) {
  const denomDiv = document.querySelector('.denomination');
  // denomination given in satoshi
  denomDiv.textContent = (denom / SATOSHI_TO_BTC);
}

function updateFee(fee) {
  const feeDiv = document.querySelector('.fee');
  feeDiv.textContent = (fee / SATOSHI_TO_BTC);
}

function updateBlockHeight(height) {
  const blockHeightDiv = document.querySelector('.block-height');
  blockHeightDiv.textContent = height;
}

function updateInputs(solvers) {
  const inputsDiv = document.querySelector('.inputs ul');

  inputsDiv.innerHTML = solvers.map(solver =>
    genSolverComponent(solver)
  ).join('');
}

function updateOutputs(promises) {
  const outputsDiv = document.querySelector('.outputs ul');

  outputsDiv.innerHTML = promises.map(promise =>
    genPromiseComponent(promise)
  ).join('');
}

var promiseVm = new Vue({
  props: ['promise']
});
var solverVm = new Vue({
  props: ['solver']
});

var cycleVm = new Vue({
  components: {
    'promise': promiseVm,
    'solver': solverVm
  },
  template: ``
});

function updateCycles(cycles) {
  const cyclesDiv = document.querySelector('.cycles ul');

  cyclesDiv.innerHTML = cycles.map(cycle =>
    genCycleComponent(cycle)
  ).join('');
}

function genSolverComponent(solver) {
  const status = solver.status;
  const id = solver.escrowedCoin.scriptPubKey;
  return `
    <li class="solver">
      <p>scriptPubKey: ${id}</p>
      <label>status: ${status}</label>
      <progress value="${solverServerSessionStates[status]}" max="6">
    </li>
  `;
}

function genPromiseComponent(promise) {
  const status = promise.status;
  const id = promise.escrowedCoin.scriptPubKey;
  return `
    <li class="promise">
      <p>scriptPubKey: ${id}</p>
      <label>status: ${status}</label>
      <progress value="${promiseServerSessionStates[status]}" max="4">
    </li>
  `;
}

function genCycleComponent(cycle) {
  const height = cycle.height; 
  return `
    <li class="cycle" id="cycle-${height}">
      <h4>Cycle ${height}</h4>
      <div class="inputs flex">
        <ul></ul>
      </div>
      <div class="outputs flex">
        <ul></ul>
      </div>
    </li>
  `;
}
